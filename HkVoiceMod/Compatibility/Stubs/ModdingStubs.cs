#if HKVOICE_STUBS
using System.Collections.Generic;

namespace Modding
{
    public interface IGlobalSettings<T>
    {
        void OnLoadGlobal(T settings);

        T OnSaveGlobal();
    }

    public abstract class Mod
    {
        protected Mod(string? name = null)
        {
            Name = string.IsNullOrEmpty(name) ? GetType().Name : name ?? GetType().Name;
        }

        public string Name { get; }

        public virtual string GetVersion()
        {
            return "STUB";
        }

        public virtual void Initialize()
        {
        }

        public virtual void Initialize(Dictionary<string, Dictionary<string, UnityEngine.GameObject>> preloadedObjects)
        {
            Initialize();
        }

        protected void Log(string message)
        {
            UnityEngine.Debug.Log($"[{Name}] {message}");
        }
    }
}
#endif
