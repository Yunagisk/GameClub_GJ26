using UnityEngine;

namespace GameClub.Player
{
    public interface IGravityExchange
    {
        Vector2 gravityDir { get; }
        bool isExchangeCooldown { get; }
        void ExchangeGravity(IGravityExchange target);

        // Commit the received direction and start this participant's own cooldown.
        void ReceiveGravity(Vector2 direction);
    }
}
