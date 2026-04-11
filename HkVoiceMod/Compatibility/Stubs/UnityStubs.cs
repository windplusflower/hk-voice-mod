#if HKVOICE_STUBS
using System;
using System.IO;

namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(object target)
        {
        }

        public static void Destroy(object target)
        {
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; } = null!;
    }

    public class MonoBehaviour : Component
    {
    }

    public sealed class GameObject : Object
    {
        public GameObject(string name = "")
        {
            this.name = name;
        }

        public string name { get; }

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T
            {
                gameObject = this
            };
            return component;
        }
    }

    public struct Vector2
    {
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float x;
        public float y;
    }

    public static class Time
    {
        private static readonly DateTime ProcessStartUtc = DateTime.UtcNow;

        public static float realtimeSinceStartup => (float)(DateTime.UtcNow - ProcessStartUtc).TotalSeconds;

        public static float unscaledDeltaTime => 1f / 60f;
    }

    public static class Debug
    {
        public static void Log(object message)
        {
            Console.WriteLine(message);
        }

        public static void LogWarning(object message)
        {
            Console.WriteLine(message);
        }

        public static void LogError(object message)
        {
            Console.Error.WriteLine(message);
        }
    }

    public static class Application
    {
        public static string persistentDataPath => Path.GetTempPath();
    }
}
#endif
