#if HKVOICE_STUBS
using InControl;

public sealed class InputHandler
{
    public static InputHandler? Instance { get; set; } = new InputHandler();

    public HeroActions inputActions { get; set; } = new HeroActions();
}

public sealed class HeroActions
{
    public HeroActions()
    {
        left = new PlayerAction("Left");
        right = new PlayerAction("Right");
        up = new PlayerAction("Up");
        down = new PlayerAction("Down");
        attack = new PlayerAction("Attack");
        jump = new PlayerAction("Jump");
        dash = new PlayerAction("Dash");
        cast = new PlayerAction("Cast");
        moveVector = new PlayerTwoAxisAction();
    }

    public PlayerAction left { get; }

    public PlayerAction right { get; }

    public PlayerAction up { get; }

    public PlayerAction down { get; }

    public PlayerAction attack { get; }

    public PlayerAction jump { get; }

    public PlayerAction dash { get; }

    public PlayerAction cast { get; }

    public PlayerTwoAxisAction moveVector { get; }
}

namespace InControl
{
    public static class InputManager
    {
        public static ulong CurrentTick { get; set; }
    }

    public sealed class PlayerAction
    {
        private bool _lastState;
        private bool _state;

        public PlayerAction(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool IsPressed => _state;

        public bool WasPressed => !_lastState && _state;

        public ulong UpdateTick { get; private set; }

        public void CommitWithState(bool state, ulong updateTick, float deltaTime)
        {
            _lastState = _state;
            _state = state;
            UpdateTick = updateTick;
        }
    }

    public sealed class PlayerTwoAxisAction
    {
        public UnityEngine.Vector2 Vector { get; private set; }

        internal void UpdateWithAxes(float x, float y, ulong updateTick, float deltaTime)
        {
            Vector = new UnityEngine.Vector2(x, y);
        }
    }
}
#endif
