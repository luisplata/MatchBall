using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance
{
    public interface ICombinationRule
    {
        /// <summary>
        /// Intenta aplicar la regla en el índice proporcionado. Devuelve true si la regla se aplicó y realizó side-effects.
        /// </summary>
        UniTask<bool> TryApplyAsync(IBalance balance, int index);
    }
}
