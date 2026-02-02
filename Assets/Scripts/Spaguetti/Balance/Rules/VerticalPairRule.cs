using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance.Rules
{
    public class VerticalPairRule : ICombinationRule
    {
        private readonly bool _useLeftPath;
        private readonly IMusicController _music;
        private readonly IDelayProvider _delay;

        public VerticalPairRule(bool useLeftPath, IMusicController music, IDelayProvider delay)
        {
            _useLeftPath = useLeftPath;
            _music = music;
            _delay = delay;
        }

        public async UniTask<bool> TryApplyAsync(IBalance balance, int index)
        {
            var path = _useLeftPath ? balance.GetLeftPath() : balance.GetRightPath();

            if (path.TryGetBallInPosition(index, out var ballLeftRow))
            {
                if (path.TryGetBallInPosition(index + 2, out var ballLeftRowAbove))
                {
                    if (MatchHelper.Matches(ballLeftRow.GetId(), ballLeftRowAbove.GetId()))
                    {
                        path.RemoveBall(index);
                        path.RemoveBall(index + 2);
                        _music?.PlaySfx("pop");
                        await path.MoveBallsDownByIndexAsc(2, index);
                        await _delay.DelaySeconds(.5f);
                        balance.AddPoints();
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
