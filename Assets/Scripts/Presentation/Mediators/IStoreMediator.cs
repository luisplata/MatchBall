using Cysharp.Threading.Tasks;
using Presentation.ViewModels;

namespace Presentation.Mediators
{
    public interface IStoreMediator
    {
        UniTask BuyItem(ItemStoreDto storeItemData);
    }
}