// Метка на враге: из какого EnemyDefinition он создан. Ставит EntitySpawner.
// Нужна тем, кто смотрит на врагов со стороны (полоски HP, босс-бар), — геймплейным скриптам врага она не нужна.
using UnityEngine;

namespace CatTanks
{
    public class EnemyTag : MonoBehaviour
    {
        public EnemyDefinition definition;

        public bool IsBoss => definition != null && definition.boss;
    }
}
