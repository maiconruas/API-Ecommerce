﻿using ApiDotNet.Application.DTOs;
using ApiDotNet.Application.DTOs.Validation;
using ApiDotNet.Application.Services.Interfaces;
using ApiDotNet.Domain.Entities;
using ApiDotNet.Domain.Repositories;
using AutoMapper;

namespace ApiDotNet.Application.Services
{
	public class PurchaseService : IPurchaseService
	{
		private readonly IProductRepository _productRepository;
		private readonly IPersonRepository _personRepository;
		private readonly IPurchaseRepository _purchaseRepository;
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _unitOfWork;

		public PurchaseService(IProductRepository productRepository, IPersonRepository personRepository, IPurchaseRepository purchaseRepository, IMapper mapper = null, IUnitOfWork unitOfWork = null)
		{
			_productRepository = productRepository;
			_personRepository = personRepository;
			_purchaseRepository = purchaseRepository;
			_mapper = mapper;
			_unitOfWork = unitOfWork;
		}

		public async Task<ResultService<PurchaseDTO>> CreateAsync(PurchaseDTO purchaseDTO)
		{
			{
				if (purchaseDTO == null)
					return ResultService.Fail<PurchaseDTO>("Objeto deve ser informado");

				var result = new PurchaseDTOValidation().Validate(purchaseDTO);
				if (!result.IsValid)
					return ResultService.RequestError<PurchaseDTO>("Problema de validacao!", result);

				try
				{
					await _unitOfWork.BeginTransaction();
					var productId = await _productRepository.GetIdByCodErpAsync(purchaseDTO.CodErp);
					if (productId == 0)
					{
						var product = new Product(purchaseDTO.ProductName, purchaseDTO.CodErp, purchaseDTO.Price ?? 0);
						await _productRepository.CreateAsync(product);
						productId = product.Id;
						;
					}

					var personId = await _personRepository.GetIdByDocumentAsync(purchaseDTO.Document);
					var purchase = new Purchase(productId, personId);

					var data = await _purchaseRepository.CreateAsync(purchase);
					purchaseDTO.Id = data.Id;
					await _unitOfWork.Commit();
					return ResultService.Ok<PurchaseDTO>(purchaseDTO);
				}
				catch(Exception ex)
				{
					await _unitOfWork.RollBack();
					return ResultService.Fail<PurchaseDTO>($"Erro: {ex.Message}");
				}
			}
		}

		public async Task<ResultService<ICollection<PurchaseDetailDTO>>> GetAsync()
		{
			var purchases = await _purchaseRepository.GetAllAsync();
			return ResultService.Ok(_mapper.Map<ICollection<PurchaseDetailDTO>>(purchases));
		}

		public async Task<ResultService<PurchaseDetailDTO>> GetByIdAsync(int id)
		{
			var purchase = await _purchaseRepository.GetByIdAsync(id);
			if (purchase == null)
				return ResultService.Fail<PurchaseDetailDTO>("Compra não encontrada");

			return ResultService.Ok(_mapper.Map<Purchase, PurchaseDetailDTO>(purchase));
		}

		public async Task<ResultService> RemoveAsync(int id)
		{
			var purchase = await _purchaseRepository.GetByIdAsync(id);
			if (purchase == null)
				return ResultService.Fail("Compra não encontrada");

			await _purchaseRepository.DeleteAsync(purchase);
			return ResultService.Ok($"Compra: {id} deletada");
		}

		public async Task<ResultService<PurchaseDTO>> UpdateAsync(PurchaseDTO purchaseDTO)
		{
			if (purchaseDTO == null)
				return ResultService.Fail<PurchaseDTO>("Objeto deve ser informado");

			var result = new PurchaseDTOValidation().Validate(purchaseDTO);
			if (!result.IsValid)
				return ResultService.RequestError<PurchaseDTO>("Problema de validacao!", result);

			var purchase = await _purchaseRepository.GetByIdAsync(purchaseDTO.Id);
			if (purchase == null)
				return ResultService.Fail<PurchaseDTO>("Compra não encontrada");

			var productId = await _productRepository.GetIdByCodErpAsync(purchaseDTO.CodErp);
			var personId = await _personRepository.GetIdByDocumentAsync(purchaseDTO.Document);
			purchase.Edit(purchase.Id, productId, personId);
			await _purchaseRepository.EditAsync(purchase);
			return ResultService.Ok(purchaseDTO);
		}
	}
}
