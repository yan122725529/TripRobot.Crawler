namespace Perst.Assoc
{
    using System;
    using Perst;

    /// <summary>
    /// Class used to specify desired order of result query
    /// </summary>
    public class OrderBy
    {
        public String name;
        public IterationOrder order;

        /// <summary>
        /// Constructor of order-by component with default (ascending) sort order
        /// </summary>
        /// <param name="name">attribute name</param>
        public OrderBy(String name)
            : this(name, IterationOrder.AscentOrder)
        {
        }

        /// <summary>
        /// Constructor of order-by component with specified sort order
        /// </summary>    
        /// <param name="name">attribute name</param>
        /// <param name="order">sort ascending or descebding sort order</param>
        public OrderBy(String name, IterationOrder order)
        {
            this.name = name;
            this.order = order;
        }
    }
}