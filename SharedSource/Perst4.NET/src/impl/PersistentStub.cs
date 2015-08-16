namespace Perst.Impl
{
    using Perst;

    public class PersistentStub : IPersistent 
    { 
        public virtual int Oid
        {
            get
            {
                return oid;
            }	
        }

        public virtual Storage Storage
        {
            get
            {
                return storage;
            }			
        }

        public virtual void Load()
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }
		
        public bool IsRaw() 
        { 
            return true;
        } 
    
        public bool IsModified() 
        { 
            return false;
        } 
 
    
        public bool IsDeleted() 
        { 
            return false;
        } 

        public bool IsPersistent()
        {
            return true;
        }
		
        public virtual int MakePersistent(Storage storage)
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }
		
        public virtual void Store()
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }
		
        public void Modify() 
        { 
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }

        public virtual void Deallocate()
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }
		
        public virtual bool RecursiveLoading()
        {
            return true;
        }
		
		
        public override bool Equals(object o)
        {
            return Storage.GetOid(o) == oid;
        }
		
        public override int GetHashCode()
        {
            return oid;
        }
		
        public virtual void OnLoad() 
        {
        }
        
        public virtual void OnStore() 
        {
        }
        
        public virtual void Invalidate() 
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }
        
        internal PersistentStub(Storage storage, int oid) 
        {
            this.storage = storage;
            this.oid = oid;
        } 

        public void UnassignOid()
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }

        public void AssignOid(Storage storage, int oid, bool raw)
        {
            throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
        }

        public virtual object Clone()
        {
            PersistentStub obj = (PersistentStub)base.MemberwiseClone();
            obj.oid = 0;
            return obj;
        }


        private Storage storage;
        private int oid;
    }
}
