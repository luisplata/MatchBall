using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance.Rules
{
    public class HorizontalPairRule : ICombinationRule
    {
        private readonly IMusicController _music;
        private readonly IDelayProvider _delay;

        public HorizontalPairRule(IMusicController music, IDelayProvider delay)
        {
            _music = music;
            _delay = delay;
        }

        public async UniTask<bool> TryApplyAsync(IBalance balance, int index)
        {
            if (balance.GetLeftPath().TryGetBallInPosition(index, out var ballLeft) &&
                balance.GetRightPath().TryGetBallInPosition(index, out var ballRight))
            {
                if (MatchHelper.Matches(ballLeft.GetId(), ballRight.GetId()))
                {
                    balance.GetLeftPath().RemoveBall(index);
                    balance.GetRightPath().RemoveBall(index);
                    _music?.PlaySfx("pop");
                    await balance.MoveBallsDownByIndexAsc(2, index);
                    await _delay.DelaySeconds(.5f);
                    balance.AwardMatchPoints(2);
                    return true;
                }
            }

            return false;
        }
    }
}
