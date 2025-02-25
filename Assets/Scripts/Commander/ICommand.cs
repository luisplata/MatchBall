using Cysharp.Threading.Tasks;

namespace Commander
{
    public interface ICommand
    {
        UniTask Execute();
    }
}