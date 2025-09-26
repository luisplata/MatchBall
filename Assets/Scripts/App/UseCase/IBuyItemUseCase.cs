using Cysharp.Threading.Tasks;

namespace App.UseCase
{
    public interface IBuyItemUseCase
    {
        UniTask Execute(string purchaseId);
    }
}