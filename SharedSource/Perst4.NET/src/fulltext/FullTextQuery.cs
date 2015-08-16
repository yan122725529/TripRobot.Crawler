using System;
namespace Perst.FullText
{
    
    /// <summary> Base class for full test search query nodes.
    /// Query can be parsed by FullTextSearchHelper class or explicitly created by user.
    /// </summary>
    public class FullTextQuery
    {
        public enum Operator
        {
            Match,
            StrictMatch,
            And,
            Near,
            Or,
            Not
        };
        
        public Operator op;
        
        /// <summary> Query node visitor.
        /// It provides convenient way of iterating through query nodes.
        /// </summary>
        public virtual void  Visit(FullTextQueryVisitor visitor)
        {
            visitor.Visit(this);
        }
        
        /// <summary> This method checks that query can be executed by interection of keyword occurrences lists</summary>
        /// <returns> true if quuery can be executed by FullTextIndex, false otherwise
        /// </returns>
        public virtual bool IsConstrained
        {
            get
            {
                return false;
            }
        }
        
        /// <summary> Query node constructor</summary>
        /// <param name="op">operation code
        /// </param>
        public FullTextQuery(Operator op)
        {
            this.op = op;
        }
    }

    /// <summary> Binary node of full text query</summary>
    public class FullTextQueryBinaryOp:FullTextQuery
    {
        public FullTextQuery left;
        public FullTextQuery right;
        
        /// <summary> Query node visitor.</summary>
        public override void  Visit(FullTextQueryVisitor visitor)
        {
            visitor.Visit(this);
            left.Visit(visitor);
            right.Visit(visitor);
        }
        
        /// <summary> This method checks that query can be executed by interection of keyword occurrences lists</summary>
        /// <returns> true if quuery can be executed by FullTextIndex, false otherwise
        /// </returns>
        public override bool IsConstrained        
        {
            get
            {
                return op == Operator.Or
                    ? left.IsConstrained && right.IsConstrained
                    : left.IsConstrained || right.IsConstrained;
            }
        }
        
        public override string ToString()
        {
            return op == Operator.Or
                ? "(" + left.ToString() + ") OR (" + right.ToString() + ")"
                : left.ToString() + " " + op + " " + right.ToString();
        }
        
        /// <summary> Binary node constructor</summary>
        /// <param name="op">operation code
        /// </param>
        /// <param name="left">left operand
        /// </param>
        /// <param name="right">right operand
        /// </param>
        public FullTextQueryBinaryOp(Operator op, FullTextQuery left, FullTextQuery right):base(op)
        {
            this.left = left;
            this.right = right;
        }
    }
    /// <summary> Match node of full text query</summary>
    public class FullTextQueryMatchOp:FullTextQuery
    {
        /// <summary> Matched word (shown be lowercvases and in normal form, unless used in quotes)</summary>
        public string word;
        
        /// <summary> Position of word in the query (zero based)</summary>
        public int pos;
        
        /// <summary> Index of the word in query (set and used internally, should not be accessed by application)</summary>
        public int wno;
        
        
        /// <summary> Query node visitor.</summary>
        public override void  Visit(FullTextQueryVisitor visitor)
        {
            visitor.Visit(this);
        }
        
        /// <summary> Match node provides query constraint</summary>
        public override bool IsConstrained
        {
            get
            {
                return true;
            }
        }
        
        public override string ToString()
        {
            return op == Operator.Match ? word : '"' + word + '"';
        }
        
        
        /// <summary> Match node constructor</summary>
        /// <param name="op">operation code (should ne MATCH or STICT_MATCH)
        /// </param>
        /// <param name="word">searched word
        /// </param>
        /// <param name="pos">position of word in the query
        /// </param>
        public FullTextQueryMatchOp(Operator op, System.String word, int pos):base(op)
        {
            this.word = word;
            this.pos = pos;
        }
    }
    /// <summary> Unary node of full text query</summary>
    public class FullTextQueryUnaryOp:FullTextQuery
    {
        public FullTextQuery opd;
        
        /// <summary> Query node visitor.</summary>
        public override void Visit(FullTextQueryVisitor visitor)
        {
            visitor.Visit(this);
            opd.Visit(visitor);
        }
        
        /// <summary> This method checks that query can be executed by interection of keyword occurrences lists</summary>
        /// <returns> true if quuery can be executed by FullTextIndex, false otherwise
        /// </returns>
        public override bool IsConstrained
        {
            get
            {
                return op == Operator.Not ? false : opd.IsConstrained;
            }
        }
        
        public override string ToString()
        {
            return op.ToString() + "(" + opd.ToString() + ")";
        }
        
        /// <summary> Unary node constructor</summary>
        /// <param name="op">operation code
        /// </param>
        /// <param name="opd">operand
        /// </param>
        public FullTextQueryUnaryOp(Operator op, FullTextQuery opd):base(op)
        {
            this.opd = opd;
        }
    }

    /// <summary> Base class for full text query visitor</summary>
    public class FullTextQueryVisitor
    {
        public virtual void  Visit(FullTextQuery q)
        {
        }
        
        public virtual void  Visit(FullTextQueryBinaryOp q)
        {
            Visit((FullTextQuery) q);
        }
        
        public virtual void  Visit(FullTextQueryUnaryOp q)
        {
            Visit((FullTextQuery) q);
        }
        
        public virtual void  Visit(FullTextQueryMatchOp q)
        {
            Visit((FullTextQuery) q);
        }
    }
}