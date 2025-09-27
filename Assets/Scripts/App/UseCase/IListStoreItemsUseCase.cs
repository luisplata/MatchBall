using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Domain.Model;

namespace App.UseCase
{
    public interface IListStoreItemsUseCase
    {
        UniTask<List<PurchaseOption>> Execute();
    }
}