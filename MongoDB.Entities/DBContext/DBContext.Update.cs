﻿namespace MongoDB.Entities
{
    public partial class DBContext
    {
        /// <summary>
        /// Starts an update command for the given entity type
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        public virtual Update<T> Update<T>() where T : IEntity
        {
            var update = new Update<T>(session);
            if (Cache<T>.ModifiedByProp != null)
            {
                ThrowIfModifiedByIsEmpty<T>();
                update.Modify(b => b.Set(Cache<T>.ModifiedByProp.Name, ModifiedBy));
            }
            return update;
        }

        /// <summary>
        /// Starts an update-and-get command for the given entity type
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        public virtual UpdateAndGet<T> UpdateAndGet<T>() where T : IEntity
        {
            var upGet = new UpdateAndGet<T>(session);
            if (Cache<T>.ModifiedByProp != null)
            {
                ThrowIfModifiedByIsEmpty<T>();
                upGet.Modify(b => b.Set(Cache<T>.ModifiedByProp.Name, ModifiedBy));
            }
            return upGet;
        }

        /// <summary>
        /// Starts an update-and-get command with projection support for the given entity type
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        /// <typeparam name="TProjection">The type of the end result</typeparam>
        public virtual UpdateAndGet<T, TProjection> UpdateAndGet<T, TProjection>() where T : IEntity
        {
            var upGet = new UpdateAndGet<T, TProjection>(session);
            if (Cache<T>.ModifiedByProp != null)
            {
                ThrowIfModifiedByIsEmpty<T>();
                upGet.Modify(b => b.Set(Cache<T>.ModifiedByProp.Name, ModifiedBy));
            }
            return upGet;
        }
    }
}
