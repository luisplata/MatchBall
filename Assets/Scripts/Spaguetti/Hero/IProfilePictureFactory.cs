using UnityEngine;

namespace Spaguetti.Hero
{
    public interface IProfilePictureFactory
    {
        Sprite GetProfilePicture(int heroId);
    }
}