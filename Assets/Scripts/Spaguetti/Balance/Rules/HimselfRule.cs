using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance.Rules
{
    public class HimselfRule : ICombinationRule
    {
        private readonly IMusicController _music;
        private readonly IDelayProvider _delay;

        public HimselfRule(IMusicController music, IDelayProvider delay)
        {
            _music = music;
            _delay = delay;
        }

        public async UniTask<bool> TryApplyAsync(IBalance balance, int index)
        {
            // Left path
            var leftPath = balance.GetLeftPath();
            if (leftPath.TryGetBallInPosition(index, out var ballLeftHs))
            {
                if (ballLeftHs.GetId() == "himself")
                {
                    leftPath.RemoveBall(index);
                    _music?.PlaySfx("pop");
                    await leftPath.MoveBallsDownByIndexAsc(1, index);
                    await _delay.DelaySeconds(.5f);
                    balance.AwardMatchPoints(1);
                    return true;
                }
            }

            // Right path
            var rightPath = balance.GetRightPath();
            if (rightPath.TryGetBallInPosition(index, out var ballRightHs))
            {
                if (ballRightHs.GetId() == "himself")
                {
                    rightPath.RemoveBall(index);
                    _music?.PlaySfx("pop");
                    await rightPath.MoveBallsDownByIndexAsc(1, index);
                    await _delay.DelaySeconds(.5f);
                    balance.AwardMatchPoints(1);
                    return true;
                }
            }

            return false;
        }
    }
}
