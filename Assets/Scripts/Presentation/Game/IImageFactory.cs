using UnityEngine;

namespace Presentation.Game
{
    public interface IImageFactory
    {
        Sprite GetSprite(string id);
    }
}