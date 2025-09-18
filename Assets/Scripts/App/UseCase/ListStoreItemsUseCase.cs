using System.Collections.Generic;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Model;

namespace App.UseCase
{
    public class ListStoreItemsUseCase : IListStoreItemsUseCase
    {
        private readonly IStoreRepository _storeRepository;

        public ListStoreItemsUseCase(IStoreRepository storeRepository)
        {
            _storeRepository = storeRepository;
        }

        public async UniTask<List<PurchaseOption>> Execute()
        {
            return await _storeRepository.GetAvailablePurchases();
        }
    }
}