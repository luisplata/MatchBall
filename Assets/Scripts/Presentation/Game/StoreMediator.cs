using System;
using System.Collections.Generic;
using App.UseCase;
using Cysharp.Threading.Tasks;
using Domain.Model;
using Presentation.Mediators;
using Presentation.UI;
using Presentation.ViewModels;
using UnityEngine;

namespace Presentation.Game
{
    public class StoreMediator : MonoBehaviour, IStoreMediator, IWaitToLoadInfo
    {
        [SerializeField] private StoreItem storeItem;
        [SerializeField] private Transform storeItemsParent;
        [SerializeField] private ImagesFactory imagesFactory;
        private List<PurchaseOption> _storeItemsData;
        private List<StoreItem> _storeItems = new();

        private async void Start()
        {
            var useCase = ServiceLocator.Instance.GetService<IListStoreItemsUseCase>();
            var purchases = await useCase.Execute();

            // Mapeamos a DTO para UI
            var globalService = ServiceLocator.Instance.GetService<IGlobalSessionService>();

            var dtos = StoreItemMapper.ToDtoList(purchases, imagesFactory, globalService);

            // limpiar la UI
            foreach (var item in _storeItems)
            {
                Destroy(item.gameObject);
            }

            _storeItems.Clear();

            // crear tarjetas
            foreach (var dto in dtos)
            {
                var item = Instantiate(storeItem, storeItemsParent);
                item.Configure(this, dto);
                _storeItems.Add(item);
            }

            IsReady = true;
        }

        public async UniTask BuyItem(ItemStoreDto storeItemData)
        {
            try
            {
                await ServiceLocator.Instance.GetService<IBuyItemUseCase>().Execute(storeItemData.PurchaseId);
                ServiceLocator.Instance.GetService<IMessageService>()
                    .ShowMessage($"Compraste {storeItemData.howMuchPerBuy} de {storeItemData.PurchaseName}").Forget();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage(e).Forget();
            }
        }

        public bool IsReady { get; private set; }
    }
}