using System.Collections.Generic;
using UnityEngine;

namespace Spaguetti.Hero
{
    [CreateAssetMenu(menuName = "Hero/list", fileName = "HeroList", order = 0)]
    public class HeroList : ScriptableObject
    {
        public List<Hero> heroes;
    }
}