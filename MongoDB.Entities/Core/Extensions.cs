﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MongoDB.Entities
{
    /// <summary>
    /// Extension methods for entities
    /// </summary>
    public static class Extensions
    {
        private class Holder<T>
        {
            public T Data { get; set; }
        }

        private static T Duplicate<T>(this T source)
        {
            return BsonSerializer.Deserialize<Holder<T>>(
                new Holder<T> { Data = source }.ToBson()
                ).Data;
        }

        internal static void ThrowIfUnsaved(this string entityID)
        {
            if (string.IsNullOrWhiteSpace(entityID))
                throw new InvalidOperationException("Please save the entity before performing this operation!");
        }

        internal static void ThrowIfUnsaved(this IEntity entity)
        {
            ThrowIfUnsaved(entity.ID);
        }

        /// <summary>
        /// Extension method for processing collections in batches with streaming (yield return)
        /// </summary>
        /// <typeparam name="T">The type of the objects inside the source collection</typeparam>
        /// <param name="collection">The source collection</param>
        /// <param name="batchSize">The size of each batch</param>
        public static IEnumerable<IEnumerable<T>> ToBatches<T>(this IEnumerable<T> collection, int batchSize = 100)
        {
            var batch = new List<T>(batchSize);

            foreach (T item in collection)
            {
                batch.Add(item);
                if (batch.Count == batchSize)
                {
                    yield return batch;
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
                yield return batch;
        }

        /// <summary>
        /// Gets the IMongoDatabase for the given entity type
        /// </summary>
        /// <typeparam name="T">The type of entity</typeparam>
        public static IMongoDatabase Database<T>(this T _) where T : IEntity => DB.Database<T>();

        /// <summary>
        /// Gets the name of the database this entity is attached to. Returns name of default database if not specifically attached.
        /// </summary>
        public static string DatabaseName<T>(this T _) where T : IEntity => DB.DatabaseName<T>();

        /// <summary>
        /// Gets the IMongoCollection for a given IEntity type.
        /// <para>TIP: Try never to use this unless really neccessary.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        public static IMongoCollection<T> Collection<T>(this T _) where T : IEntity => DB.Collection<T>();

        /// <summary>
        /// Gets the collection name for this entity
        /// </summary>
        public static string CollectionName<T>(this T _) where T : IEntity
        {
            return DB.CollectionName<T>();
        }

        /// <summary>
        /// Returns the full dotted path of a property for the given expression
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        public static string FullPath<T>(this Expression<Func<T, object>> expression)
        {
            return Prop.Path(expression);
        }

        /// <summary>
        /// An IQueryable collection of sibling Entities.
        /// </summary>
        public static IMongoQueryable<T> Queryable<T>(this T _, AggregateOptions options = null) where T : IEntity
        {
            return DB.Queryable<T>(options);
        }

        /// <summary>
        /// An IAggregateFluent collection of sibling Entities.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="options">The options for the aggregation. This is not required.</param>
        public static IAggregateFluent<T> Fluent<T>(this T _, IClientSessionHandle session = null, AggregateOptions options = null) where T : IEntity
        {
            return DB.Fluent<T>(options, session);
        }

        /// <summary>
        /// Adds a distinct aggregation stage to a fluent pipeline.
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        public static IAggregateFluent<T> Distinct<T>(this IAggregateFluent<T> aggregate) where T : IEntity
        {
            PipelineStageDefinition<T, T> groupStage = @"
                                                        {
                                                            $group: {
                                                                _id: '$_id',
                                                                doc: {
                                                                    $first: '$$ROOT'
                                                                }
                                                            }
                                                        }";

            PipelineStageDefinition<T, T> rootStage = @"
                                                        {
                                                            $replaceRoot: {
                                                                newRoot: '$doc'
                                                            }
                                                        }";

            return aggregate.AppendStage(groupStage).AppendStage(rootStage);
        }

        /// <summary>
        /// Appends a match stage to the pipeline with a filter expression
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="aggregate"></param>
        /// <param name="filter">f => f.Eq(x => x.Prop, Value) &amp; f.Gt(x => x.Prop, Value)</param>
        public static IAggregateFluent<T> Match<T>(this IAggregateFluent<T> aggregate, Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter) where T : IEntity
        {
            return aggregate.Match(filter(Builders<T>.Filter));
        }

        /// <summary>
        /// Appends a match stage to the pipeline with an aggregation expression (i.e. $expr)
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="aggregate"></param>
        /// <param name="expression">{ $gt: ['$Property1', '$Property2'] }</param>
        public static IAggregateFluent<T> MatchExpression<T>(this IAggregateFluent<T> aggregate, string expression) where T : IEntity
        {
            PipelineStageDefinition<T, T> stage = "{$match:{$expr:" + expression + "}}";

            return aggregate.AppendStage(stage);
        }

        /// <summary>
        /// Returns a reference to this entity.
        /// </summary>
        public static One<T> ToReference<T>(this T entity) where T : IEntity
        {
            return new One<T>(entity);
        }

        /// <summary>
        /// Creates an unlinked duplicate of the original IEntity ready for embedding with a blank ID.
        /// </summary>
        public static T ToDocument<T>(this T entity) where T : IEntity
        {
            var res = entity.Duplicate();
            res.ID = res.GenerateNewID();
            return res;
        }

        /// <summary>
        /// Creates unlinked duplicates of the original Entities ready for embedding with blank IDs.
        /// </summary>
        public static T[] ToDocuments<T>(this T[] entities) where T : IEntity
        {
            var res = entities.Duplicate();
            foreach (var e in res)
                e.ID = e.GenerateNewID();
            return res;
        }

        /// <summary>
        ///Creates unlinked duplicates of the original Entities ready for embedding with blank IDs.
        /// </summary>
        public static IEnumerable<T> ToDocuments<T>(this IEnumerable<T> entities) where T : IEntity
        {
            var res = entities.Duplicate();
            foreach (var e in res)
                e.ID = e.GenerateNewID();
            return res;
        }

        /// <summary>
        /// Saves a complete entity replacing an existing entity or creating a new one if it does not exist. 
        /// If ID value is null, a new entity is created. If ID has a value, then existing entity is replaced.
        /// </summary>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task SaveAsync<T>(this T entity, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SaveAsync(entity, session, cancellation);
        }

        /// <summary>
        /// Saves a batch of complete entities replacing existing ones or creating new ones if they do not exist. 
        /// If ID value is null, a new entity is created. If ID has a value, then existing entity is replaced.
        /// </summary>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task<BulkWriteResult<T>> SaveAsync<T>(this IEnumerable<T> entities, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SaveAsync(entities, session, cancellation);
        }

        /// <summary>
        /// Saves an entity partially with only the specified subset of properties. 
        /// If ID value is null, a new entity is created. If ID has a value, then existing entity is updated.
        /// <para>TIP: The properties to be saved can be specified with a 'New' expression. 
        /// You can only specify root level properties with the expression.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="entity">The entity to save</param>
        /// <param name="members">x => new { x.PropOne, x.PropTwo }</param>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task<UpdateResult> SaveOnlyAsync<T>(this T entity, Expression<Func<T, object>> members, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SaveOnlyAsync(entity, members, session, cancellation);
        }

        /// <summary>
        /// Saves a batch of entities partially with only the specified subset of properties. 
        /// If ID value is null, a new entity is created. If ID has a value, then existing entity is updated.
        /// <para>TIP: The properties to be saved can be specified with a 'New' expression. 
        /// You can only specify root level properties with the expression.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="entities">The batch of entities to save</param>
        /// <param name="members">x => new { x.PropOne, x.PropTwo }</param>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task<BulkWriteResult<T>> SaveOnlyAsync<T>(this IEnumerable<T> entities, Expression<Func<T, object>> members, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SaveOnlyAsync(entities, members, session, cancellation);
        }

        /// <summary>
        /// Saves an entity partially excluding the specified subset of properties. 
        /// If ID value is null, a new entity is created. If ID has a value, then existing entity is updated.
        /// <para>TIP: The properties to be excluded can be specified with a 'New' expression. 
        /// You can only specify root level properties with the expression.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="entity">The entity to save</param>
        /// <param name="members">x => new { x.PropOne, x.PropTwo }</param>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task<UpdateResult> SaveExceptAsync<T>(this T entity, Expression<Func<T, object>> members, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SaveExceptAsync(entity, members, session, cancellation);
        }

        /// <summary>
        /// Saves a batch of entities partially excluding the specified subset of properties. 
        /// If ID value is null, a new entity is created. If ID has a value, then existing entity is updated.
        /// <para>TIP: The properties to be excluded can be specified with a 'New' expression. 
        /// You can only specify root level properties with the expression.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="entities">The batch of entities to save</param>
        /// <param name="members">x => new { x.PropOne, x.PropTwo }</param>
        /// <param name="session">An optional session if using within a transaction</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task<BulkWriteResult<T>> SaveExceptAsync<T>(this IEnumerable<T> entities, Expression<Func<T, object>> members, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SaveExceptAsync(entities, members, session, cancellation);
        }

        /// <summary>
        /// Saves an entity partially while excluding some properties. 
        /// The properties to be excluded can be specified using the [Preserve] attribute.
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="entity">The entity to save</param>
        /// <param name="cancellation">An optional cancellation token</param>
        public static Task<UpdateResult> SavePreservingAsync<T>(this T entity, IClientSessionHandle session = null, CancellationToken cancellation = default) where T : IEntity
        {
            return DB.SavePreservingAsync(entity, session, cancellation);
        }

        /// <summary>
        /// Deletes a single entity from MongoDB.
        /// <para>HINT: If this entity is referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
        /// </summary>
        public static Task<DeleteResult> DeleteAsync<T>(this T entity, IClientSessionHandle session = null) where T : IEntity
        {
            return DB.DeleteAsync<T>(entity.ID, session);
        }

        /// <summary>
        /// Deletes multiple entities from the database
        /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
        /// </summary>
        public static Task<DeleteResult> DeleteAllAsync<T>(this IEnumerable<T> entities, IClientSessionHandle session = null) where T : IEntity
        {
            return DB.DeleteAsync<T>(entities.Select(e => e.ID), session);
        }

        /// <summary>
        /// Sort a list of objects by relevance to a given string using Levenshtein Distance
        /// </summary>
        /// <typeparam name="T">Any object type</typeparam>
        /// <param name="objects">The list of objects to sort</param>
        /// <param name="searchTerm">The term to measure relevance to</param>
        /// <param name="propertyToSortBy">x => x.PropertyName [the term will be matched against the value of this property]</param>
        /// <param name="maxDistance">The maximum levenstein distance to qualify an item for inclusion in the returned list</param>
        public static IEnumerable<T> SortByRelevance<T>(this IEnumerable<T> objects, string searchTerm, Func<T, string> propertyToSortBy, int? maxDistance = null)
        {
            var lev = new Levenshtein(searchTerm);

            var res = objects.Select(o => new
            {
                score = lev.DistanceFrom(propertyToSortBy(o)),
                obj = o
            });

            if (maxDistance.HasValue)
                res = res.Where(x => x.score <= maxDistance.Value);

            return res.OrderBy(x => x.score)
                      .Select(x => x.obj);
        }

        /// <summary>
        /// Converts a search term to Double Metaphone hash code suitable for fuzzy text searching.
        /// </summary>
        /// <param name="term">A single or multiple word search term</param>
        public static string ToDoubleMetaphoneHash(this string term)
        {
            return string.Join(" ", DoubleMetaphone.GetKeys(term));
        }

        /// <summary>
        /// Returns an atomically generated sequential number for the given Entity type everytime the method is called
        /// </summary>
        /// <typeparam name="T">The type of entity to get the next sequential number for</typeparam>
        public static Task<ulong> NextSequentialNumberAsync<T>(this T _) where T : IEntity
        {
            return DB.NextSequentialNumberAsync<T>();
        }

        /// <summary>
        /// Initializes supplied property with a new One-To-Many relationship.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="propertyToInit">() => PropertyName</param>
        public static void InitOneToMany<TChild>(this IEntity parent, Expression<Func<Many<TChild>>> propertyToInit) where TChild : IEntity
        {
            var property = (((propertyToInit.Body as UnaryExpression)?.Operand ?? propertyToInit.Body) as MemberExpression)?.Member as PropertyInfo;
            property.SetValue(parent, new Many<TChild>(parent, property.Name));
        }

        /// <summary>
        /// Initializes supplied property with a new Many-To-Many relationship.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="propertyToInit">() = > PropertyName</param>
        /// <param name="propertyOtherSide">x => x.PropertyName</param>
        public static void InitManyToMany<TChild>(this IEntity parent, Expression<Func<Many<TChild>>> propertyToInit, Expression<Func<TChild, object>> propertyOtherSide) where TChild : IEntity
        {
            var property = (((propertyToInit.Body as UnaryExpression)?.Operand ?? propertyToInit.Body) as MemberExpression)?.Member as PropertyInfo;
            var hasOwnerAttrib = property.IsDefined(typeof(OwnerSideAttribute), false);
            var hasInverseAttrib = property.IsDefined(typeof(InverseSideAttribute), false);
            if (hasOwnerAttrib && hasInverseAttrib) throw new InvalidOperationException("Only one type of relationship side attribute is allowed on a property");
            if (!hasOwnerAttrib && !hasInverseAttrib) throw new InvalidOperationException("Missing attribute for determining relationship side of a many-to-many relationship");

            var osProperty = (((propertyOtherSide.Body as UnaryExpression)?.Operand ?? propertyOtherSide.Body) as MemberExpression)?.Member;
            var osHasOwnerAttrib = osProperty.IsDefined(typeof(OwnerSideAttribute), false);
            var osHasInverseAttrib = osProperty.IsDefined(typeof(InverseSideAttribute), false);
            if (osHasOwnerAttrib && osHasInverseAttrib) throw new InvalidOperationException("Only one type of relationship side attribute is allowed on a property");
            if (!osHasOwnerAttrib && !osHasInverseAttrib) throw new InvalidOperationException("Missing attribute for determining relationship side of a many-to-many relationship");

            if ((hasOwnerAttrib == osHasOwnerAttrib) || (hasInverseAttrib == osHasInverseAttrib)) throw new InvalidOperationException("Both sides of the relationship cannot have the same attribute");

            property.SetValue(parent, new Many<TChild>(parent, property.Name, osProperty.Name, hasInverseAttrib));
        }

        /// <summary>
        /// Pings the mongodb server to check if it's still connectable
        /// </summary>
        /// <param name="timeoutSeconds">The number of seconds to keep trying</param>
        public static async Task<bool> IsAccessibleAsync(this IMongoDatabase db, int timeoutSeconds = 5)
        {
            using (var cts = new CancellationTokenSource(timeoutSeconds * 1000))
            {
                try
                {
                    var res = await db.RunCommandAsync((Command<BsonDocument>)"{ping:1}", null, cts.Token).ConfigureAwait(false);
                    return res["ok"] == 1;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Checks to see if the database already exists on the mongodb server
        /// </summary>
        /// <param name="timeoutSeconds">The number of seconds to keep trying</param>
        public static async Task<bool> ExistsAsync(this IMongoDatabase db, int timeoutSeconds = 5)
        {
            using (var cts = new CancellationTokenSource(timeoutSeconds * 1000))
            {
                try
                {
                    var dbs = await (await db.Client.ListDatabaseNamesAsync(cts.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
                    return dbs.Contains(db.DatabaseNamespace.DatabaseName);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
