using System;
using System.Linq;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;
using TripRobot.Crawler.Etity.RedisEntity;


namespace TripRobot.Crawler.Redis
{
    public class RedisRepository<TEntity> :
        IDisposable
        //IRepository<TEntity>
        where TEntity : RedisEntity
    {
        private readonly IRedisClient redisDB;
        private IRedisTypedClient<TEntity> redisTypedClient;
        private IRedisList<TEntity> table;

        public RedisRepository()
        {
            redisDB =RedisManager.GetClient();
            redisTypedClient = redisDB.As<TEntity>();
            table = redisTypedClient.Lists[typeof (TEntity).Name];
        }

        #region IDisposable成员

        public void Dispose()
        {
            ExplicitDispose();
        }

        #endregion

        #region Finalization Constructs

        /// <summary>
        ///     Finalizes the object.
        /// </summary>
        ~RedisRepository()
        {
            Dispose(false);
        }

        #endregion

        #region IRepository<TEntity>成员

        
        public TEntity MoveNext()
        {
           return redisTypedClient.PopItemFromList(table);
        }

        public void Insert(TEntity item)
        {
            if (item != null)
            {
                
                redisTypedClient.AddItemToList(table, item);
                redisDB.SaveAsync();
            }
        }

        public void Delete(TEntity item)
        {
            if (item != null)
            {
                var entity = Find(item.Key);
                redisTypedClient.RemoveItemFromList(table, entity);
                redisDB.SaveAsync();
            }
        }

        public void Update(TEntity item)
        {
            if (item != null)
            {
                var old = Find(item.Key);
                if (old != null)
                {
                    redisTypedClient.RemoveItemFromList(table, old);
                    redisTypedClient.AddItemToList(table, item);
                    redisDB.SaveAsync();
                }
            }
        }

        public IQueryable<TEntity> GetModel()
        {
            return table.GetAll().AsQueryable();
        }

        public TEntity Find(params object[] id)
        {
            return table.FirstOrDefault(i => i.Key == (string) id[0]);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        ///     Provides the facility that disposes the object in an explicit manner,
        ///     preventing the Finalizer from being called after the object has been
        ///     disposed explicitly.
        /// </summary>
        protected void ExplicitDispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing) //清除非托管资源
            {
                table = null;
                redisTypedClient = null;
                redisDB.Dispose();
            }
        }

        #endregion
    }
}