namespace Perst.Impl
{
    using Perst;

    public interface OidHashTable { 
        bool        remove(int oid);
        void        put(int oid, object obj);
        object      get(int oid);
        void        flush();
        void        invalidate();
        void        reload();
        void        clear();
        int         size();
        void        setDirty(object obj);
        void        clearDirty(object obj);
    }
}
