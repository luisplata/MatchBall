using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Spaguetti.Hero
{
    public interface IHeroController
    {
        UniTask LoadHeroes();
    }
}