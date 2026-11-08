using UnityEngine;

namespace GameClub.Player
{
    public interface IGravityExchange
    {
        Vector2 gravityDir { get; }
        bool isExchangeCooldown { get; }
        void ExchangeGravity(IGravityExchange target);

        void ReceiveGravity(Vector2 direction);
    }
}
