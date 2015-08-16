namespace Perst
{
    using System;
    using System.Collections;
#if USE_GENERICS || NET_FRAMEWORK_35
    using System.Collections.Generic;
#endif


    /// <summary> 
    /// Class extent lock type for execution of preapred statements
    /// </summary> 
    public enum ClassExtentLockType 
    { 
        None,
        Shared,
        Exclusive
    };

    /// <summary> 
    /// Interface used by Query to get index for the specified key
    /// </summary> 
    public interface IndexProvider 
    { 
        /// <summary> 
        /// Get index for the specified field of the class
        /// </summary> 
        /// <param name="type">class where index is located</param>
        /// <param name="key">field of the class</param>
        /// <returns>Index for this field or null if index doesn't exist</returns>
        GenericIndex GetIndex(Type type, string key);
    }

    /// <summary> 
    /// Class representing JSQL query. JSQL allows to select members of Perst collections 
    /// using SQL like predicate. Almost all Perst collections have select() method 
    /// which execute arbitrary JSQL query. But it is also possible to create Query instance explicitely, 
    /// Using storage.createQuery class. In this case it is possible to specify query with parameters, 
    /// once prepare query and then multiple times specify parameters and execute it. 
    /// Also Query interface allows to specify <i>indices</i> and <i>resolvers</i>.
    /// JSQL can use arbitrary Perst <see cref="Perst.GenericIndex"/> to perform fast selection if object
    /// instead of sequeial search. And user provided <i>resolver</i> allows to substitute SQL joins.
    /// </summary>
#if USE_GENERICS
    public interface Query<T> : IEnumerable<T>
#else
    public interface Query : IEnumerable
#endif
    {
#if USE_GENERICS
        /// <summary> Execute query
        /// </summary>
        /// <param name="e">enumerable collection for sequential access to objects in the table
        /// </param>
        /// <param name="predicate">selection crieria
        /// </param>
        /// <returns> 
        /// iterator through selected objects
        /// </returns>
        IEnumerable<T> Select(IEnumerable<T> e, string predicate);

        /// <summary> Prepare SQL statement
        /// </summary>
        /// <param name="predicate">selection crieria with '?' placeholders for parameter value
        /// </param>
        void  Prepare(string predicate);

        /// <summary> Execute prepared query using iterator obtained from index registered by Query.SetClassExtent method
        /// </summary>
        /// <returns> iterator through selected objects
        /// </returns>
        IEnumerable<T> Execute();

        /// <summary> Execute prepared query
        /// </summary>
        /// <param name="iterator">iterator for sequential and direct access to objects in the table
        /// </param>
        /// <returns> iterator through selected objects
        /// </returns>
        IEnumerable<T> Execute(IEnumerable<T> iterator);
#else
#if NET_FRAMEWORK_35
        /// <summary> Execute query
        /// </summary>
        /// <param name="e">enumerable collection for sequential access to objects in the table
        /// </param>
        /// <param name="predicate">selection crieria
        /// </param>
        /// <returns> 
        /// iterator through selected objects
        /// </returns>
        IEnumerable<T> Select<T>(IEnumerable e, string predicate) where T:class;

        /// <summary> Execute prepared query
        /// </summary>
        /// <param name="iterator">iterator for sequential and direct access to objects in the table
        /// </param>
        /// <returns> iterator through selected objects
        /// </returns>
        IEnumerable<T> Execute<T>(IEnumerable iterator) where T:class;
#endif        
        /// <summary> Execute query
        /// </summary>
        /// <param name="cls">class of inspected objects
        /// </param>
        /// <param name="e">enumerable collection for sequential access to objects in the table
        /// </param>
        /// <param name="predicate">selection crieria
        /// </param>
        /// <returns> 
        /// iterator through selected objects
        /// </returns>
        IEnumerable Select(Type cls, IEnumerable e, string predicate);

        /// <summary> Execute query
        /// </summary>
        /// <param name="className">name of the class of inspected objects
        /// </param>
        /// <param name="e">enumerable collection for sequential access to objects in the table
        /// </param>
        /// <param name="predicate">selection crieria
        /// </param>
        /// <returns> iterator through selected objects
        /// </returns>
        IEnumerable Select(string className, IEnumerable e, string predicate);

        /// <summary> Prepare SQL statement
        /// </summary>
        /// <param name="cls">class of iterated objects
        /// </param>
        /// <param name="predicate">selection crieria with '?' placeholders for parameter value
        /// </param>
        void  Prepare(Type cls, string predicate);

        /// <summary> Prepare SQL statement
        /// </summary>
        /// <param name="className">name of the class of iterated objects
        /// </param>
        /// <param name="predicate">selection crieria with '?' placeholders for parameter value
        /// </param>
        void  Prepare(string className, string predicate);

        /// <summary> Execute prepared query using iterator obtained from index registered by Query.SetClassExtent method
        /// </summary>
        /// <returns> iterator through selected objects
        /// </returns>
        IEnumerable Execute();

        /// <summary> Execute prepared query
        /// </summary>
        /// <param name="iterator">iterator for sequential and direct access to objects in the table
        /// </param>
        /// <returns> iterator through selected objects
        /// </returns>
        IEnumerable Execute(IEnumerable iterator);
#endif

        /// <summary>Set or get value of query parameter
        /// </summary>
        /// <param name="i">parameters index (1 based)
        /// </param>
        object this[int i]
        {
            get;
            set;
        }
               
        /// <summary> Enable or disable reporting of runtime errors on console.
        /// Runtime errors during JSQL query are reported in two ways:
        /// <OL>
        /// <LI>If query error reporting is enabled then message is  printed to System.err</LI>
        /// <LI>If storage listener is registered, then JSQLRuntimeError of method listener is invoked</LI>
        /// </OL>     
        /// By default reporting to System.err is enabled.
        /// </summary>
        /// <param name="enabled">if <b>true</b> then reportnig is enabled
        /// </param>
        void  EnableRuntimeErrorReporting(bool enabled);

        /// <summary> Specify resolver. Resolver can be used to replaced SQL JOINs: given object ID, 
        /// it will provide reference to the resolved object
        /// </summary>
        /// <param name="original">class which instances will have to be resolved
        /// </param>
        /// <param name="resolved">class of the resolved object
        /// </param>
        /// <param name="resolver">class implementing Resolver interface
        /// </param>
        void  SetResolver(Type original, Type resolved, Resolver resolver);

        /// <summary> Add index which can be used to optimize query execution (replace sequential search with direct index access)
        /// </summary>
        /// <param name="key">indexed field
        /// </param>
        /// <param name="index">implementation of index
        /// </param>
#if USE_GENERICS
        void  AddIndex(string key, GenericKeyIndex<T> index);
#else
        void  AddIndex(string key, GenericIndex index);
#endif

        /// <summary>
        /// Set index provider for this query.
        /// Available indices shoudl be either registered using addIndex method, either 
        /// should be accessible through index provider
        /// </summary>
        /// <param name="indexProvider">index provider</param>
        ///
        void SetIndexProvider(IndexProvider indexProvider);

        /// <summary>
        /// Set class for which this query will be executed
        /// </summary>
        /// <param name="type">queried class</param>
        void SetClass(Type type);


        /// <summary>
        /// Set class extent used to obtain iterator through all instances of this class
        /// </summary>
        /// <param name="set">class extent</param>
        /// <param name="lockType">type of the lock which should be obtained for the set before query execution</param>
        ///
#if USE_GENERICS
        void SetClassExtent(IEnumerable<T> set, ClassExtentLockType lockType);
#else
        void SetClassExtent(IEnumerable set, ClassExtentLockType lockType);
#endif

        /// <summary>
        /// Get query code generator for the specified class
        /// </summary>
        /// <param name="cls">class for which query is constructed</param>
        /// <returns>code generator for the specified class</returns>
        ///
        CodeGenerator GetCodeGenerator(Type cls);

        /// <summary>
        /// Get query code generator for class associated with the query by Query.setClass method
        /// </summary>
        /// <returns>code generator for class associated with the query</returns>
        ///
        CodeGenerator GetCodeGenerator();
    }
}