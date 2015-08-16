namespace Perst.Assoc
{
    using System;

    /// <summary>
    /// Class used to construct search query
    /// </summary>
    public class Predicate
    {
        /// <summary>
        /// Conjunction: Logical AND of two conditions
        /// </summary>
        public class And : Predicate
        {
            /// <summary>
            /// Logical AND constructor
            /// </summary>
            /// <param name="left">left condition</param>
            /// <param name="right">right condition</param>
            public And(Predicate left, Predicate right)
            {
                this.left = left;
                this.right = right;
            }
            public Predicate left;
            public Predicate right;
        }

        /// <summary>
        /// Disjunction: Logical OR of two conditions
        /// </summary>
        public class Or : Predicate
        {
            /// <summary>
            /// Logical OR constructor
            /// </summary>
            /// <param name="left">left condition</param>
            /// <param name="right">right condition</param>
            public Or(Predicate left, Predicate right)
            {
                this.left = left;
                this.right = right;
            }
            public Predicate left;
            public Predicate right;
        }

        /// <summary>
        /// Compare operation: compares attribute value with specified constant
        /// Please notice that operation rather than Equals may requires sorting of selection of result
        /// is used in logical AND or OR operations (it can influence on query execution time and memory demands)
        /// </summary>
        public class Compare : Predicate
        {
            /// <summary>
            /// Comparison operations
            /// </summary>
            public enum Operation
            {
                Equals,
                LessThan,
                LessOrEquals,
                GreaterThan,
                GreaterOrEquals,
                StartsWith,
                IsPrefixOf,
                InArray
            }

            /// <summary>
            /// Constructor of comparison operation
            /// </summary>
            /// <param name="name">attribute name</param>
            /// <param name="oper">comparison operation</param>
            /// <param name="value">compared value</param>
            public Compare(string name, Operation oper, object value)
            {
                this.name = name;
                this.oper = oper;
                this.value = value;
            }
            public string name;
            public Operation oper;
            public object value;
        }

        /// <summary>
        /// Between operation
        /// </summary>
        public class Between : Predicate
        {
            /// <summary>
            /// Constructor of between operation
            /// </summary>
            /// <param name="name">attribute name</param>
            /// <param name="from">minimal value (inclusive)</param>
            /// <param name="till">maximal value (inclusive)</param>
            public Between(string name, object from, object till)
            {
                this.name = name;
                this.from = from;
                this.till = till;
            }
            public string name;
            public object from;
            public object till;
        }

        /// <summary>
        /// Equivalent of SQL IN operator
        /// </summary>
        public class In : Predicate
        {
            /// <summary>
            /// Constructor of In operator
            /// </summary>
            /// <param name="name">attribute name</param>
            /// <param name="subquery">nested subquery</param>
            public In(string name, Predicate subquery)
            {
                this.name = name;
                this.subquery = subquery;
            }
            public string name;
            public Predicate subquery;
        }

        /// <summary>
        /// Full text search query
        /// </summary>
        public class Match : Predicate
        {
            /// <summary>
            /// Constructor of full text search query
            /// </summary>
            /// <param name="query">text of query. Full text index is able to execute search queries with logical operators (AND/OR/NOT) and 
            /// strict match. Returned results are ordered by rank, which includes inverse document frequency (IDF),
            /// frequency of word in the document, occurrence kind and nearness of query keywords in the document text
            /// </param>
            /// <param name="maxResults">maximal amount of selected documents</param>
            /// <param name="timeLimit">limit for query execution time</param>
            public Match(string query, int maxResults, int timeLimit)
            {
                this.query = query;
                this.maxResults = maxResults;
                this.timeLimit = timeLimit;
            }
            public string query;
            public int maxResults;
            public int timeLimit;
        }

        /// <summary>
        /// Construct Logical AND operation
        /// </summary>
        /// <param name="left">left condition</param>
        /// <param name="right">right condition</param>
        public static Predicate operator &(Predicate left, Predicate right)
        {
            return new And(left, right);
        }

        /// <summary>
        /// Construct Logical OR operation
        /// </summary>
        /// <param name="left">left condition</param>
        /// <param name="right">right condition</param>
        public static Predicate operator |(Predicate left, Predicate right)
        {
            return new Or(left, right);
        }

        /// <summary>
        /// Reference to item's attribute
        /// </summary>
        public class Attr
        {
            /// <summary>
            /// Constructor of attribute reference
            /// </summary>
            /// <param name="name">attribute name</param>
            public Attr(string name)
            {
                this.name = name;
            }
            public string name;

            public static Predicate operator ==(Attr attr, object value)
            {
                return new Compare(attr.name, Compare.Operation.Equals, value);
            }

            public static Predicate operator !=(Attr attr, object value)
            {
                throw new InvalidOperationException("!= operator is not defined");
            }

            public static Predicate operator >(Attr attr, object value)
            {
                return new Compare(attr.name, Compare.Operation.GreaterThan, value);
            }

            public static Predicate operator >=(Attr attr, object value)
            {
                return new Compare(attr.name, Compare.Operation.GreaterOrEquals, value);
            }

            public static Predicate operator <(Attr attr, object value)
            {
                return new Compare(attr.name, Compare.Operation.LessThan, value);
            }

            public static Predicate operator <=(Attr attr, object value)
            {
                return new Compare(attr.name, Compare.Operation.LessOrEquals, value);
            }

            public Predicate Between(object from, object till)
            {
                return new Between(name, from, till);
            }

            public Predicate StartsWith(string prefix)
            {
                return new Compare(name, Compare.Operation.StartsWith, prefix);
            }

            public Predicate IsPrefixOf(string text)
            {
                return new Compare(name, Compare.Operation.IsPrefixOf, text);
            }

            public Predicate In(Predicate subquery)
            {
                return new In(name, subquery);
            }

            public Predicate In(string[] arr)
            {
                return new Compare(name, Compare.Operation.InArray, arr);
            }

            public Predicate In(double[] arr)
            {
                return new Compare(name, Compare.Operation.InArray, arr);
            }

            public Predicate In(Item[] arr)
            {
                return new Compare(name, Compare.Operation.InArray, arr);
            }
        }

        /// <summary>
        /// Construct reference to the attrbute value
        /// </summary>
        /// <param name="name">attribute name</param>
        public static Attr Value(string name)
        {
            return new Attr(name);
        }

        /// <summary>
        /// Construct full text search query
        /// @param query text of query. Full text index is able to execute search queries with logical operators (AND/OR/NOT) and 
        /// strict match. Returned results are ordered by rank, which includes inverse document frequency (IDF),
        /// frequency of word in the document, occurrence kind and nearness of query keywords in the document text
        /// </summary>
        /// <param name="maxResults">maximal amount of selected documents</param>
        /// <param name="timeLimit">limit for query execution time</param>
        public static Predicate Contains(string query, int maxResults, int timeLimit)
        {
            return new Match(query, maxResults, timeLimit);
        }
    }
}