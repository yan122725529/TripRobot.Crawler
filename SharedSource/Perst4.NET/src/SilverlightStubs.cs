using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Perst
{
#if WINRT_NET_FRAMEWORK
    class AppDomain
    {
        public static AppDomain CurrentDomain { get; private set; }

        static AppDomain()
        {
            CurrentDomain = new AppDomain();
        }

        public Assembly[] GetAssemblies()
        {
            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            List<Assembly> assemblies = new List<Assembly>();
            System.Threading.Tasks.Task<IReadOnlyList<Windows.Storage.StorageFile>> t = folder.GetFilesAsync().AsTask<IReadOnlyList<Windows.Storage.StorageFile>>();
            t.Wait();
            foreach (Windows.Storage.StorageFile file in t.Result)
            {
                if (file.FileType == ".dll" || file.FileType == ".exe")
                {
                    AssemblyName name = new AssemblyName() { Name = file.DisplayName };
                    Assembly asm = Assembly.Load(name);
                    assemblies.Add(asm);
                }
            }

            return assemblies.ToArray();
        }
    }

    public class Thread 
    {
        static System.Threading.ThreadLocal<Thread> currentThread = new System.Threading.ThreadLocal<Thread>();
        public static Thread CurrentThread
        {
            get
            {
                if (!currentThread.IsValueCreated)
                {
                    currentThread.Value = new Thread();
                }
                return currentThread.Value;
                //System.Environment.CurrentManagedThreadId();
            }
        }
    }
#endif
#if SILVERLIGHT
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
    public class NonSerializedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class SerializableAttribute : Attribute
    {
    }

    public class ArrayList : List<object>
    {
        public object[] ToArray(Type elemType)
        {
            object[] arr = (object[])Array.CreateInstance(elemType, Count);
            CopyTo(arr);
            return arr;
        }

        public void AddRange(System.Collections.ICollection col)
        {
            base.AddRange((IEnumerable<object>)col);
        }
    }   

    public class Hashtable : Dictionary<object, object>
    {
        public new object this[object key]
        {
            get
            {
                object v = null;
                TryGetValue(key, out v);
                return v;
            }
            set
            {
                base[key] = value;
            }
        }

        public bool Contains(object key)
        {
            return ContainsKey(key);
        }
    }

#endif
}
