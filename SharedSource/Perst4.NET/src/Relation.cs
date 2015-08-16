namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#else
    using System.Collections;
#endif

    /// <summary> Class representing relation between owner and members
    /// </summary>
#if USE_GENERICS
    public abstract class Relation<M,O> : PersistentCollection<M>, Link<M> where M:class where O:class
#else
    public abstract class Relation:PersistentCollection, Link
#endif
    {
        public abstract int Size();

        public abstract int Length 
        {
            get;
            set;
        }

#if USE_GENERICS
        public abstract M this[int i] 
#else
        public abstract object this[int i] 
#endif
        {
            get;
            set;
        }
		
#if USE_GENERICS
        public abstract M Get(int i);
#else
        public abstract object Get(int i);
#endif
		
        public abstract object GetRaw(int i);
		
#if USE_GENERICS
        public abstract void  Set(int i, M obj);
#else
        public abstract void  Set(int i, object obj);
#endif
		
#if !USE_GENERICS
        public abstract bool  Remove(object obj);
#endif

#if USE_GENERICS
        public abstract void  RemoveAt(int i);
#endif
        public abstract void  Remove(int i);

#if USE_GENERICS
        public abstract void  Insert(int i, M obj);
#else
        public abstract void  Insert(int i, object obj);
#endif
		
#if !USE_GENERICS
        public abstract void  Add(object obj);
#endif
		
#if USE_GENERICS
        public abstract void  AddAll(M[] arr);
#else
        public abstract void  AddAll(object[] arr);
#endif
		
#if USE_GENERICS
        public abstract void  AddAll(M[] arr, int from, int length);
#else
        public abstract void  AddAll(object[] arr, int from, int length);
#endif
		
#if USE_GENERICS
        public abstract void  AddAll(Link<M> anotherLink);
#else
        public abstract void  AddAll(Link anotherLink);
#endif		
      
#if USE_GENERICS
        public abstract M[] ToArray();
#else
        public abstract object[] ToArray();
#endif

        public abstract Array ToRawArray();

        public abstract Array ToArray(Type elemType);

#if !USE_GENERICS
        public abstract bool  Contains(object obj);
#endif
		
#if USE_GENERICS
        public abstract bool  ContainsElement(int i, M obj);
#else
        public abstract bool  ContainsElement(int i, object obj);
#endif

#if USE_GENERICS
        public abstract int   IndexOf(M obj);
#else
        public abstract int   IndexOf(object obj);
#endif
		
        public abstract void  Pin();

        public abstract void  Unpin();
 

#if USE_GENERICS
        public abstract IBidirectionalEnumerator<M> GetEnumerator(int start);
#else
        public abstract IBidirectionalEnumerator GetEnumerator(int start);
#endif

        /// <summary>Get/Set relation owner
        /// </summary>
#if USE_GENERICS
        public virtual O Owner
#else
        public virtual object Owner
#endif
        {
            get
            {
                return owner;
            }
			
            set
            {
                this.owner = value;
                Modify();
            }			
        }

        /// <summary> Relation constructor. Creates empty relation with specified owner and no members. 
        /// Members can be added to the relation later.
        /// </summary>
        /// <param name="owner">owner of the relation
        /// 
        /// </param>		
#if USE_GENERICS
        public Relation(O owner)
#else
        public Relation(object owner)
#endif
        {
            this.owner = owner;
        }
		
        internal Relation() {}

        public void SetOwner(object obj)
        { 
#if USE_GENERICS
             owner = (O)obj;
#else
             owner = obj;
#endif
             Modify();
        }

#if USE_GENERICS
        internal O owner;
#else
        internal object owner;
#endif
    }
}