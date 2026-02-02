using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Spaguetti.Balance.Rules;

namespace Spaguetti.Balance
{
    public class CheckerOfBallsLogic : ICheckerOfBallsToGetPoints
    {
        private readonly IEnumerable<ICombinationRule> _rules;
        private readonly IDelayProvider _delay;

        // Constructor para inyección de dependencias
        public CheckerOfBallsLogic(IEnumerable<ICombinationRule> rules, IDelayProvider delay)
        {
            _rules = rules ?? throw new System.ArgumentNullException(nameof(rules));
            _delay = delay ?? new UniTaskDelayProvider();
        }

        // Constructor por defecto para compatibilidad (usa ServiceLocator)
        public CheckerOfBallsLogic() : this(DefaultRulesFactory.Create(ServiceLocator.Instance.GetService<IMusicController>(), new UniTaskDelayProvider()), new UniTaskDelayProvider())
        {
        }

        public async UniTask<bool> HaveCombinations(IBalance _balance)
        {
            if (_balance == null)
            {
                UnityEngine.Debug.LogError("CheckerOfBallsLogic.HaveCombinations: balance is null");
                return false;
            }

            int columns = _balance.CountOfColumns();
            for (int i = 0; i < columns; i++)
            {
                foreach (var rule in _rules)
                {
                    try
                    {
                        if (await rule.TryApplyAsync(_balance, i))
                        {
                            return true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogError($"Error applying rule {rule.GetType().Name} at index {i}: {ex}");
                    }
                }
            }

            return false;
        }
    }


    public interface ICheckerOfBallsToGetPoints
    {
        UniTask<bool> HaveCombinations(IBalance _balance);
    }
}