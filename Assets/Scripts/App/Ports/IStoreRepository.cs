using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Domain.Model;

namespace App.Ports
{
    public interface IStoreRepository
    {
        UniTask<List<PurchaseOption>> GetAvailablePurchases();
    }
}