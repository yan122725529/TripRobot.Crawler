namespace Perst.Impl
{
    using System;
#if USE_GENERICS || NET_FRAMEWORK_35
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Reflection;
    using System.Diagnostics;
	
#if USE_GENERICS
    class FilterIterator
#else 
    class FilterIterator : IEnumerable, IEnumerator
#endif
    {
        internal IEnumerator enumerator;
        internal QueryImpl query;
        internal object    currObj;
        internal Node      condition;
        internal int[]     indexVar;
        internal long[]    intAggragateFuncValue;
        internal double[]  realAggragateFuncValue;
        internal object[]  containsArray;
        internal object    containsElem;
		
        internal const int maxIndexVars = 32;
		
        internal FilterIterator(object obj) 
        { 
            currObj = obj;
        }

        internal FilterIterator() {}
#if USE_GENERICS
    }
    class FilterIterator<T> : FilterIterator, IEnumerable<T>, IEnumerator<T>, IEnumerable, IEnumerator
    {
#endif 

        public bool MoveNext()
        {
            while (enumerator.MoveNext())
            {
                currObj = enumerator.Current;
#if WINRT_NET_FRAMEWORK
                if (currObj != null && query.cls.GetTypeInfo().IsAssignableFrom(currObj.GetType().GetTypeInfo()))
#else
                if (query.cls.IsInstanceOfType(currObj))
#endif
                { 
                    if (condition == null) 
                    { 
                        return true;
                    }
                    try
                    {
                        if (condition.evaluateBool(this))
                        {
                            return true;
                        }
                    }
                    catch (JSQLRuntimeException x)
                    {
                        query.ReportRuntimeError(x);
                    }
                }
                currObj = null;
            }
            return false;
        }
		
#if USE_GENERICS
        public void Dispose() 
        { 
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public IEnumerator<T> GetEnumerator() 
        {
            return this;
        }

        object IEnumerator.Current
        {
            get
            {
                return getCurrent();
            }
        }
        
        public T Current
        {
            get 
            {
                return (T)getCurrent();
            }
        }

        private object getCurrent()
        {
            if (currObj == null)
            {
                throw new InvalidOperationException();
            }
            return (T)currObj;
        }
#else
        public IEnumerator GetEnumerator() 
        {
            return this;
        }
        
        public object Current
        {
            get 
            {
                if (currObj == null)
                {
                    throw new InvalidOperationException();
                }
                return currObj;
            }
        }
#endif

        public void Reset() 
        {
            enumerator.Reset();
            currObj = null;
        }
		
        internal FilterIterator(QueryImpl query, IEnumerable enumeration, Node condition)
        {
            this.query = query;
            this.enumerator = enumeration.GetEnumerator();
            this.condition = condition;
            indexVar = new int[maxIndexVars];
        }
    }

    class UnionIterator : IEnumerable, IEnumerator
    { 
        GenericIndex index;
        IEnumerator currIterator;
        IEnumerator firstIterator;
        Type keyType;
        IEnumerator alternativesIterator;
    
        public UnionIterator(GenericIndex index, IEnumerator currIterator, IEnumerable alternatives) { 
            this.index = index;
            this.currIterator = currIterator;
            this.alternativesIterator = alternatives.GetEnumerator();
            firstIterator = currIterator;
            keyType = index.KeyType;
        }
        
        public IEnumerator GetEnumerator() 
        {
            return this;
        }
        
        public bool MoveNext() { 
            while (currIterator == null || !currIterator.MoveNext()) { 
                if (!alternativesIterator.MoveNext()) { 
                    return false;
                }
                Key key = KeyBuilder.getKeyFromObject(index.KeyType, alternativesIterator.Current);
                currIterator = index.Range(key, key, IterationOrder.AscentOrder).GetEnumerator();
            }
            return true;
        }
    
        public object Current
        {
            get 
            {
                if (currIterator == null)
                {
                    throw new InvalidOperationException();
                }
                return currIterator.Current;
            }
        }
        
        public void Reset() 
        {
            currIterator = firstIterator;
            currIterator.Reset();
            alternativesIterator.Reset();
        }             
    }
    
    class JoinIterator : IEnumerable, IEnumerator
    {
        internal GenericIndex joinIndex;
        internal IEnumerator  iterator;

        IEnumerator  joinIterator;

        public IEnumerator GetEnumerator() 
        {
            return this;
        }
        
        public bool MoveNext() 
        { 
            while (joinIterator == null || !joinIterator.MoveNext()) 
            { 
                if (!iterator.MoveNext()) 
                { 
                    return false;
                }
                Object obj = iterator.Current;
                Key key = new Key(obj);
                joinIterator = joinIndex.Range(key, key, IterationOrder.AscentOrder).GetEnumerator();
            }
            return true;
        }

        public object Current
        {
            get 
            {
                if (joinIterator == null)
                {
                    throw new InvalidOperationException();
                }
                return joinIterator.Current;
            }
        }
        
        public void Reset() 
        {
            joinIterator = null;
            iterator.Reset();
        }             
    }	
        
    internal enum NodeType 
    { 	
        tpBool,
        tpInt,
        tpReal,
        tpFreeVar,
        tpList,
        tpObj,
        tpStr,
        tpDate, 
        tpArrayBool,
        tpArrayChar,
        tpArrayInt1,
        tpArrayInt2,
        tpArrayInt4,
        tpArrayInt8,
        tpArrayUInt1,
        tpArrayUInt2,
        tpArrayUInt4,
        tpArrayUInt8,
        tpArrayReal4,
        tpArrayReal8,
        tpArrayStr,
        tpArrayObj,
        tpCollection,
        tpUnknown,
        tpAny
    };

	
    internal enum NodeTag 
    {         
        opNop,
        opIntAdd,
        opIntSub,
        opIntMul,
        opIntDiv,
        opIntAnd,
        opIntOr,
        opIntNeg,
        opIntNot,
        opIntAbs,
        opIntPow,
        opIntEq,
        opIntNe,
        opIntGt,
        opIntGe,
        opIntLt,
        opIntLe,
        opIntBetween,
		
        opRealEq,
        opRealNe,
        opRealGt,
        opRealGe,
        opRealLt,
        opRealLe,
        opRealBetween,
		
        opDateEq,
        opDateNe,
        opDateGt,
        opDateGe,
        opDateLt,
        opDateLe,
        opDateBetween,
		
        opStrEq,
        opStrNe,
        opStrGt,
        opStrGe,
        opStrLt,
        opStrLe,
        opStrBetween,
        opStrLike,
        opStrLikeEsc,
            		
        opStrIgnoreCaseEq,
        opStrIgnoreCaseNe,
        opStrIgnoreCaseGt,
        opStrIgnoreCaseGe,
        opStrIgnoreCaseLt,
        opStrIgnoreCaseLe,
        opStrIgnoreCaseBetween,
        opStrIgnoreCaseLike,
        opStrIgnoreCaseLikeEsc,

        opBoolEq,
        opBoolNe,
		
        opObjEq,
        opObjNe,
		
        opRealAdd,
        opRealSub,
        opRealMul,
        opRealDiv,
        opRealNeg,
        opRealAbs,
        opRealPow,
		
        opIntToReal,
        opRealToInt,
        opIntToStr,
        opRealToStr,
        opDateToStr,
        opStrToDate,
		
        opIsNull,
		
        opStrGetAt,
        opGetAtBool,
        opGetAtChar,
        opGetAtInt1,
        opGetAtInt2,
        opGetAtInt4,
        opGetAtInt8,
        opGetAtUInt1,
        opGetAtUInt2,
        opGetAtUInt4,
        opGetAtUInt8,
        opGetAtReal4,
        opGetAtReal8,
        opGetAtStr,
        opGetAtObj,
		
        opLength,
        opExists,
        opIndexVar,
            		
        opFalse,
        opTrue,
        opNull,
        opCurrent,
		
        opIntConst,
        opRealConst,
        opStrConst,
        opDateConst,
		
        opInvoke,
		
        opScanArrayBool,
        opScanArrayChar,
        opScanArrayInt1,
        opScanArrayInt2,
        opScanArrayInt4,
        opScanArrayInt8,
        opScanArrayUInt1,
        opScanArrayUInt2,
        opScanArrayUInt4,
        opScanArrayUInt8,
        opScanArrayReal4,
        opScanArrayReal8,
        opScanArrayStr,
        opScanArrayObj,
        opInString,
		
        opRealSin,
        opRealCos,
        opRealTan,
        opRealAsin,
        opRealAcos,
        opRealAtan,
        opRealSqrt,
        opRealExp,
        opRealLog,
        opRealCeil,
        opRealFloor,
		
        opBoolAnd,
        opBoolOr,
        opBoolNot,
		
        opStrLower,
        opStrUpper,
        opStrConcat,
        opStrLength,
		
        opLoad,
		
        opLoadAny,
        opInvokeAny,
		
        opContains,
        opElement,
		
        opAvg,
        opCount,
        opMax,
        opMin,
        opSum,
		
        opParameter,
		
        opAnyAdd,
        opAnySub,
        opAnyMul,
        opAnyDiv,
        opAnyAnd,
        opAnyOr,
        opAnyNeg,
        opAnyNot,
        opAnyAbs,
        opAnyPow,
        opAnyEq,
        opAnyNe,
        opAnyGt,
        opAnyGe,
        opAnyLt,
        opAnyLe,
        opAnyBetween,
        opAnyLength,
        opInAny,
        opAnyToStr,
        opConvertAny,
		
        opResolve,
        opScanCollection
    };

    internal enum Token 
    {
        tknNone,
        tknIdent,
        tknLpar,
        tknRpar,
        tknLbr,
        tknRbr,
        tknDot,
        tknComma,
        tknPower,
        tknIconst,
        tknSconst,
        tknFconst,
        tknAdd,
        tknSub,
        tknMul,
        tknDiv,
        tknAnd,
        tknOr,
        tknNot,
        tknNull,
        tknNeg,
        tknEq,
        tknNe,
        tknGt,
        tknGe,
        tknLt,
        tknLe,
        tknBetween,
        tknEscape,
        tknExists,
        tknLike,
        tknIn,
        tknLength,
        tknLower,
        tknUpper,
        tknAbs,
        tknIs,
        tknInteger,
        tknReal,
        tknString,
        tknCurrent,
        tknCol,
        tknTrue,
        tknFalse,
        tknWhere,
        tknOrder,
        tknAsc,
        tknDesc,
        tknEof,
        tknSin,
        tknCos,
        tknTan,
        tknAsin,
        tknAcos,
        tknAtan,
        tknSqrt,
        tknLog,
        tknExp,
        tknCeil,
        tknFloor,
        tknBy,
        tknHaving,
        tknGroup,
        tknAvg,
        tknCount,
        tknMax,
        tknMin,
        tknSum,
        tknWith,
        tknParam,
        tknContains
    };

    class Node : Code
    {
        internal const int Unknown = int.MinValue;

        virtual internal string FieldName
        {
            get
            {
                return null;
            }
			
        }

        virtual internal FieldInfo Field
        {
            get
            {
                return null;
            }			
        }

        virtual internal Type Type
        {
            get
            {
                return null;
            }
			
        }

        virtual internal int IndirectionLevel
        {
            get
            {
                return 0;
            }
        }


        internal NodeType type;
        internal NodeTag  tag;
		
        protected static bool ArrayEquals(Array a, Array b) 
        {
#if COMPACT_NET_FRAMEWORK
            for (int i = 0, n = a.Length; i < n; i++) 
            { 
                object ai = a.GetValue(i);
                object bi = b.GetValue(i);
                if ((ai == null && bi != null) || (ai != null && !ai.Equals(bi))) 
                {
                    return false;
                }
            }
            return true;
#else
            return Array.Equals(a, b);
#endif
        }

        internal static string wrapNullString(object val) 
        {
           return val == null ? "" : (string)val;
        }

        public  override bool Equals(object o)
        {
            return o is Node && ((Node) o).tag == tag && ((Node) o).type == type;
        }
		
        internal static bool equalObjects(object a, object b)
        {
            return a == b || (a != null && a.Equals(b));
        }
		
		
        internal static NodeType getNodeType(Type type)
        {
            if (type.Equals(typeof(sbyte)) || type.Equals(typeof(short)) || type.Equals(typeof(int)) || type.Equals(typeof(long))
                || type.Equals(typeof(byte)) || type.Equals(typeof(ushort)) || type.Equals(typeof(uint)) || type.Equals(typeof(ulong)))
            {
                return NodeType.tpInt;
            }
            else if (type.Equals(typeof(bool)))
            {
                return NodeType.tpBool;
            }
            else if (type.Equals(typeof(double)) || type.Equals(typeof(float)))
            {
                return NodeType.tpReal;
            }
#if NET_FRAMEWORK_20
#if WINRT_NET_FRAMEWORK
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
#else
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
#endif
            { 
                return NodeType.tpAny;
            }
#endif
            else if (type.Equals(typeof(string)))
            {
                return NodeType.tpStr;
            }
            else if (type.Equals(typeof(DateTime)))
            {
                return NodeType.tpDate;
            }
            else if (type.Equals(typeof(bool[])))
            {
                return NodeType.tpArrayBool;
            }
            else if (type.Equals(typeof(sbyte[])))
            {
                return NodeType.tpArrayInt1;
            }
            else if (type.Equals(typeof(byte[])))
            {
                return NodeType.tpArrayUInt1;
            }
            else if (type.Equals(typeof(short[])))
            {
                return NodeType.tpArrayInt2;
            }
            else if (type.Equals(typeof(ushort[])))
            {
                return NodeType.tpArrayUInt2;
            }
            else if (type.Equals(typeof(char[])))
            {
                return NodeType.tpArrayChar;
            }
            else if (type.Equals(typeof(int[])))
            {
                return NodeType.tpArrayInt4;
            }
            else if (type.Equals(typeof(uint[])))
            {
                return NodeType.tpArrayUInt4;
            }
            else if (type.Equals(typeof(long[])))
            {
                return NodeType.tpArrayInt8;
            }
            else if (type.Equals(typeof(ulong[])))
            {
                return NodeType.tpArrayUInt8;
            }
            else if (type.Equals(typeof(float[])))
            {
                return NodeType.tpArrayReal4;
            }
            else if (type.Equals(typeof(double[])))
            {
                return NodeType.tpArrayReal8;
            }
            else if (type.Equals(typeof(string[])))
            {
                return NodeType.tpArrayStr;
            }
#if WINRT_NET_FRAMEWORK
            else if (typeof(ICollection).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
#else
            else if (typeof(ICollection).IsAssignableFrom(type))
#endif
            {
                return NodeType.tpCollection;
            }
            else if (type.IsArray)
            {
                return NodeType.tpArrayObj;
            }
            else if (type.Equals(typeof(object)))
            {
                return NodeType.tpAny;
            }
            else
            {
                return NodeType.tpObj;
            }
        }
		
		
        internal virtual bool evaluateBool(FilterIterator t)
        {
            throw new InvalidOperationException();
        }
        internal virtual long evaluateInt(FilterIterator t)
        {
            throw new InvalidOperationException();
        }
        internal virtual double evaluateReal(FilterIterator t)
        {
            throw new InvalidOperationException();
        }
        internal virtual DateTime evaluateDate(FilterIterator t)
        {
            return (DateTime) evaluateObj(t);
        }
        internal virtual string evaluateStr(FilterIterator t)
        {
            return wrapNullString(evaluateObj(t));
        }
        internal virtual object evaluateObj(FilterIterator t)
        {
            switch (type)
            {
				
                case NodeType.tpDate: 
                    return evaluateDate(t);
				
                case NodeType.tpStr: 
                    return evaluateStr(t);
				
                case NodeType.tpInt: 
                    return evaluateInt(t);
				
                case NodeType.tpReal: 
                    return evaluateReal(t);
				
                case NodeType.tpBool: 
                    return evaluateBool(t)?true:false;
				
                default: 
                    throw new InvalidOperationException();
				
            }
        }
		
        public override string ToString()
        {
            return "Node tag=" + tag + ", type=" + type;
        }
		
        internal Node(NodeType type, NodeTag tag)
        {
            this.type = type;
            this.tag = tag;
        }
    }
	
    class EmptyNode:Node
    {
        internal override bool evaluateBool(FilterIterator t)
        {
            return true;
        }
		
        internal EmptyNode():base(NodeType.tpBool, NodeTag.opTrue)
        {
        }
    }
	
    abstract class LiteralNode:Node
    {
        internal abstract object Value{get;}
 		
        internal override object evaluateObj(FilterIterator t)
        {
            return Value;
        }
		
		
        internal LiteralNode(NodeType type, NodeTag tag):base(type, tag)
        {
        }
    }
	
    class IntLiteralNode:LiteralNode
    {
        override internal object Value
        {
            get
            {
                return val;
            }
			
        }
        internal long val;
		
        public  override bool Equals(object o)
        {
            return o is IntLiteralNode && ((IntLiteralNode) o).val == val;
        }
		
		
        internal override long evaluateInt(FilterIterator t)
        {
            return val;
        }
		
        internal IntLiteralNode(long val):base(NodeType.tpInt, NodeTag.opIntConst)
        {
            this.val = val;
        }
    }
	
	
    class RealLiteralNode:LiteralNode
    {
        override internal object Value
        {
            get
            {
                return val;
            }
			
        }
        internal double val;
		
        public  override bool Equals(object o)
        {
            return o is RealLiteralNode && ((RealLiteralNode) o).val == val;
        }
		
		
        internal override double evaluateReal(FilterIterator t)
        {
            return val;
        }
		
        internal RealLiteralNode(double val):base(NodeType.tpReal, NodeTag.opRealConst)
        {
            this.val = val;
        }
    }
	
    class StrLiteralNode:LiteralNode
    {
        override internal object Value
        {
            get
            {
                return val;
            }
			
        }
        internal string val;
		
        public  override bool Equals(object o)
        {
            return o is StrLiteralNode && ((StrLiteralNode) o).val.Equals(val);
        }
		
		
        internal override string evaluateStr(FilterIterator t)
        {
            return val;
        }
		
        internal StrLiteralNode(string val):base(NodeType.tpStr, NodeTag.opStrConst)
        {
            this.val = val;
        }
    }
	
    class DateLiteralNode:LiteralNode
    {
        override internal object Value
        {
            get
            {
                return val;
            }
			
        }
        internal DateTime val;
		
        public  override bool Equals(object o)
        {
            return o is DateLiteralNode && ((DateLiteralNode) o).val.Equals(val);
        }
		
		
        internal override DateTime evaluateDate(FilterIterator t)
        {
            return val;
        }
		
        internal DateLiteralNode(DateTime val):base(NodeType.tpDate, NodeTag.opDateConst)
        {
            this.val = val;
        }
    }
	
	
    class CurrentNode:Node
    {
        override internal Type Type
        {
            get
            {
                return cls;
            }
			
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            return t.currObj;
        }
		
        internal CurrentNode(Type cls):base(NodeType.tpObj, NodeTag.opCurrent)
        {
            this.cls = cls;
        }
        internal Type cls;
    }
	
    class ConstantNode:LiteralNode
    {
        override internal object Value
        {
            get
            {
                switch (tag)
                {
					
                    case NodeTag.opNull: 
                        return null;
					
                    case NodeTag.opFalse: 
                        return false;
					
                    case NodeTag.opTrue: 
                        return true;
					
                    default: 
                        throw new Exception("Invalid tag " + tag);
					
                }
            }			
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            return tag != NodeTag.opFalse;
        }
		
        internal ConstantNode(NodeType type, NodeTag tag):base(type, tag)
        {
        }
    }
	
    class IndexOutOfRangeError:Exception
    {
        internal int loopId;
		
        internal IndexOutOfRangeError(int loop)
        {
            loopId = loop;
        }
    }
	
    class ExistsNode:Node
    {
        internal Node expr;
        internal int loopId;
		
        public  override bool Equals(object o)
        {
            return o is ExistsNode && ((ExistsNode) o).expr.Equals(expr) && ((ExistsNode) o).loopId == loopId;
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            t.indexVar[loopId] = 0;
            try
            {
                while (!expr.evaluateBool(t))
                {
                    t.indexVar[loopId] += 1;
                }
                return true;
            }
            catch (IndexOutOfRangeError x)
            {
                if (x.loopId != loopId)
                {
                    throw x;
                }
                return false;
            }
        }
		
        internal ExistsNode(Node expr, int loopId):base(NodeType.tpBool, NodeTag.opExists)
        {
            this.expr = expr;
            this.loopId = loopId;
        }
    }
	
	
    class IndexNode:Node
    {
        internal int loopId;
		
        public  override bool Equals(object o)
        {
            return o is IndexNode && ((IndexNode) o).loopId == loopId;
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            return t.indexVar[loopId];
        }
		
        internal IndexNode(int loop):base(NodeType.tpInt, NodeTag.opIndexVar)
        {
            loopId = loop;
        }
    }
	
    class GetAtNode:Node
    {
        internal Node left;
        internal Node right;
		
        public  override bool Equals(object o)
        {
            return o is GetAtNode && ((GetAtNode) o).left.Equals(left) && ((GetAtNode) o).right.Equals(right);
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            object arr = left.evaluateObj(t);
            long idx = right.evaluateInt(t);
			
            if (right.tag == NodeTag.opIndexVar)
            {
                if (idx >= ((Array) arr).Length)
                {
                    throw new IndexOutOfRangeError(((IndexNode) right).loopId);
                }
            }
            int index = (int) idx;
            switch (tag)
            {				
                case NodeTag.opGetAtInt1: 
                    return ((sbyte[]) arr)[index];
				
                case NodeTag.opGetAtInt2: 
                    return ((short[]) arr)[index];
				
                case NodeTag.opGetAtInt4: 
                    return ((int[]) arr)[index];
				
                case NodeTag.opGetAtInt8: 
                    return ((long[]) arr)[index];
				
                case NodeTag.opGetAtUInt1: 
                    return ((byte[]) arr)[index];
				
                case NodeTag.opGetAtUInt2: 
                    return ((ushort[]) arr)[index];
				
                case NodeTag.opGetAtUInt4: 
                    return ((uint[]) arr)[index];
				
                case NodeTag.opGetAtUInt8: 
                    return (long)((ulong[]) arr)[index];
				
                case NodeTag.opGetAtChar: 
                    return ((char[]) arr)[index];
				
                case NodeTag.opStrGetAt: 
                    return ((string) arr)[index];
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            object arr = left.evaluateObj(t);
            long index = right.evaluateInt(t);
			
            if (right.tag == NodeTag.opIndexVar)
            {
                if (index >= ((Array) arr).Length)
                {
                    throw new IndexOutOfRangeError(((IndexNode) right).loopId);
                }
            }
            switch (tag)
            {				
                case NodeTag.opGetAtReal4: 
                    return ((float[]) arr)[(int) index];
				
                case NodeTag.opGetAtReal8: 
                    return ((double[]) arr)[(int) index];
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            bool[] arr = (bool[]) left.evaluateObj(t);
            long index = right.evaluateInt(t);
			
            if (right.tag == NodeTag.opIndexVar)
            {
                if (index >= arr.Length)
                {
                    throw new IndexOutOfRangeError(((IndexNode) right).loopId);

                }
            }
            return arr[(int) index];
        }
		
        internal override string evaluateStr(FilterIterator t)
        {
            string[] arr = (string[]) left.evaluateObj(t);
            long index = right.evaluateInt(t);
			
            if (right.tag == NodeTag.opIndexVar)
            {
                if (index >= arr.Length)
                {
                    throw new IndexOutOfRangeError(((IndexNode) right).loopId);
                }
            }
            return wrapNullString(arr[(int) index]);
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            object arr = left.evaluateObj(t);
            long index = right.evaluateInt(t);
			
                if (right.tag == NodeTag.opIndexVar)
                {
                    if (index >= ((Array) arr).Length)
                    {
                        throw new IndexOutOfRangeError(((IndexNode) right).loopId);
                    }
                }
                return ((Array) arr).GetValue((int) index);
        }
		
        internal GetAtNode(NodeType type, NodeTag tag, Node baseExpr, Node index):base(type, tag)
        {
            left = baseExpr;
            right = index;
        }
    }
	
    class InvokeNode:Node
    {
        override internal Type Type
        {
            get
            {
                return mth.ReturnType;
            }
			
        }
        override internal string FieldName
        {
            get
            {
                string name = mth.Name;
                if (name.StartsWith("get_"))
                {  
                    name = name.Substring(4);
                } 
                if (target != null && target.tag != NodeTag.opCurrent)
                {
                    string baseName = target.FieldName;
                    return (baseName != null)?baseName + "." + name:null;
                }
                else
                {
                    return name;
                }
            }
			
        }
        internal Node target;
        internal Node[] arguments;
        internal MethodInfo mth;
		
        public  override bool Equals(object o)
        {
            return o is InvokeNode 
                && equalObjects(((InvokeNode) o).target, target) 
                && ArrayEquals(((InvokeNode) o).arguments, arguments) 
                && equalObjects(((InvokeNode) o).mth, mth);
        }
		
		
		
        internal virtual object getTarget(FilterIterator t)
        {
            if (target == null)
            {
                return t.currObj;
            }
            object obj = target.evaluateObj(t);
            if (obj == null)
            {
                throw new JSQLNullPointerException(target.Type, mth.ToString());
            }
            return obj;
        }
		
        internal virtual object[] evaluateArguments(FilterIterator t)
        {
            object[] parameters = null;
            int n = arguments.Length;
            if (n > 0)
            {
                parameters = new object[n];
                for (int i = 0; i < n; i++)
                {
                    Node arg = arguments[i];
                    object val;
                    switch (arg.type)
                    {						
                        case NodeType.tpInt: 
                            val = arg.evaluateInt(t);
                            break;
						
                        case NodeType.tpReal: 
                            val = arg.evaluateReal(t);
                            break;
						
                        case NodeType.tpStr: 
                            val = arg.evaluateStr(t);
                            break;
						
                        case NodeType.tpDate: 
                            val = arg.evaluateDate(t);
                            break;
						
                        case NodeType.tpBool: 
                            val = arg.evaluateBool(t);
                            break;
						
                        default: 
                            val = arg.evaluateObj(t);
                            break;
						
                    }
                    parameters[i] = val;
                }
            }
            return parameters;
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            object obj = getTarget(t);
            object[] parameters = evaluateArguments(t);
            return Convert.ToInt64(mth.Invoke(obj, parameters));
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            object obj = getTarget(t);
            object[] parameters = evaluateArguments(t);
            return Convert.ToDouble(mth.Invoke(obj, parameters));
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            object obj = getTarget(t);
            object[] parameters = evaluateArguments(t);
            return (bool) mth.Invoke(obj, parameters);
        }
		
        internal override string evaluateStr(FilterIterator t)
        {
            object obj = getTarget(t);
            object[] parameters = evaluateArguments(t);
            return wrapNullString(mth.Invoke(obj, parameters));
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            object obj = getTarget(t);
            object[] parameters = evaluateArguments(t);
            return mth.Invoke(obj, parameters);
        }
		
        internal InvokeNode(Node target, MethodInfo mth, Node[] arguments):base(getNodeType(mth.ReturnType), NodeTag.opInvoke)
        {
            this.target = target;
            this.arguments = arguments;
            this.mth = mth;
        }
    }
	
	
    class InvokeAnyNode:Node
    {
        override internal Type Type
        {
            get
            {
                return typeof(object);
            }
			
        }
        override internal string FieldName
        {
            get
            {
                if (target != null)
                {
                    if (target.tag != NodeTag.opCurrent)
                    {
                        string baseName = target.FieldName;
                        return (baseName != null)?baseName + "." + methodName:null;
                    }
                    else
                    {
                        return methodName;
                    }
                }
                else
                {
                    return containsFieldName != null?containsFieldName + "." + methodName:methodName;
                }
            }
			
        }
        internal Node target;
        internal Node[] arguments;
        internal Type[] profile;
        internal string methodName;
        internal string containsFieldName;
		
        public  override bool Equals(object o)
        {
            if (!(o is InvokeAnyNode))
            {
                return false;
            }
            InvokeAnyNode node = (InvokeAnyNode) o;
            return equalObjects(node.target, target) 
                && ArrayEquals(node.arguments, arguments) 
                && ArrayEquals(node.profile, profile) 
                && equalObjects(node.methodName, methodName) 
                && equalObjects(node.containsFieldName, containsFieldName);
        }
		
		
		
        internal InvokeAnyNode(Node target, string name, Node[] arguments, string containsFieldName):base(NodeType.tpAny, NodeTag.opInvokeAny)
        {
            this.target = target;
            this.containsFieldName = containsFieldName;
            methodName = name;
            this.arguments = arguments;
            profile = new Type[arguments.Length];
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            Type cls;
            MethodInfo m;
            object obj = t.currObj;
            if (target != null)
            {
                obj = target.evaluateObj(t);
                if (obj == null)
                {
                    throw new JSQLNullPointerException(null, methodName);
                }
            }
            object[] parameters = null;
            int n = arguments.Length;
            if (n > 0)
            {
                parameters = new object[n];
                for (int i = 0; i < n; i++)
                {
                    Node arg = arguments[i];
                    object val;
                    Type type;
                    switch (arg.type)
                    {
						
                        case NodeType.tpInt: 
                            val = arg.evaluateInt(t);
                            type = typeof(long);
                            break;
						
                        case NodeType.tpReal: 
                            val = arg.evaluateReal(t);
                            type = typeof(double);
                            break;
						
                        case NodeType.tpStr: 
                            val = arg.evaluateStr(t);
                            type = typeof(string);
                            break;
						
                        case NodeType.tpDate: 
                            val = arg.evaluateDate(t);
                            type = typeof(DateTime);
                            break;
						
                        case NodeType.tpBool: 
                            val = arg.evaluateBool(t);
                            type = typeof(bool);
                            break;
						
                        default: 
                            val = arg.evaluateObj(t);
                            if (val != null)
                            {
                                type = val.GetType();
                                if (type.Equals(typeof(long)) || type.Equals(typeof(int)) || type.Equals(typeof(byte)) 
                                    || type.Equals(typeof(char)) || type.Equals(typeof(short)) || type.Equals(typeof(ulong)) 
                                    || type.Equals(typeof(uint)) || type.Equals(typeof(sbyte)) || type.Equals(typeof(ushort)))
                                {
                                    type = typeof(long);
                                }
                                else if (type.Equals(typeof(float)) || type.Equals(typeof(double)))
                                {
                                    type = typeof(double);
                                }
                                else if (type.Equals(typeof(bool)))
                                {
                                    type = typeof(bool);
                                }
                            }
                            else
                            {
                                type = typeof(object);
                            }
                            break;
						
                    }
                    parameters[i] = val;
                    profile[i] = type;
                }
            }
                if (target == null && t.containsElem != null)
                {
                    if ((m = QueryImpl.lookupMethod(t.containsElem.GetType(), methodName, profile)) != null)
                    {
                        return t.query.resolve(m.Invoke(t.containsElem, (object[]) parameters));
                    }
                }
                cls = obj.GetType();
                if ((m = QueryImpl.lookupMethod(cls, methodName, profile)) != null)
                {
                    return t.query.resolve(m.Invoke(t.containsElem, (object[]) parameters));
                }
			
            throw new JSQLNoSuchFieldException(cls, methodName);
        }
    }
	
	
    class ConvertAnyNode:Node
    {
        public  override bool Equals(object o)
        {
            return o is ConvertAnyNode && base.Equals(o) && ((ConvertAnyNode) o).expr.Equals(expr);
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            object val = evaluateObj(t);
            return val is bool ? (bool)val : false;
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            return (long) evaluateObj(t);
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            object val = evaluateObj(t);
            return val == null ? Double.NaN : (double) val;
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            return expr.evaluateObj(t);
        }
		
        internal ConvertAnyNode(NodeType type, Node expr):base(type, NodeTag.opConvertAny)
        {
            this.expr = expr;
        }
        internal Node expr;
    }
	
    class BinOpNode:Node
    {
        internal Node left;
        internal Node right;
		
        public  override bool Equals(object o)
        {
            return o is BinOpNode 
                && base.Equals(o) 
                && ((BinOpNode) o).left.Equals(left) 
                && ((BinOpNode) o).right.Equals(right);
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            long lval = left.evaluateInt(t);
            long rval = right.evaluateInt(t);
            long res;
            switch (tag)
            {				
                case NodeTag.opIntAdd: 
                    return lval + rval;
				
                case NodeTag.opIntSub: 
                    return lval - rval;
				
                case NodeTag.opIntMul: 
                    return lval * rval;
				
                case NodeTag.opIntDiv: 
                    if (rval == 0)
                    {
                        throw new JSQLArithmeticException("Divided by zero");
                    }
                    return lval / rval;
				
                case NodeTag.opIntAnd: 
                    return lval & rval;
				
                case NodeTag.opIntOr: 
                    return lval | rval;
				
                case NodeTag.opIntPow: 
                    res = 1;
                    if (rval < 0)
                    {
                        lval = 1 / lval;
                        rval = - rval;
                    }
                    while (rval != 0)
                    {
                        if ((rval & 1) != 0)
                        {
                            res *= lval;
                        }
                        lval *= lval;
                        rval = (long)((ulong)rval >> 1);
                    }
                    return res;
				
                default: 
                    throw new Exception("Invalid tag");				
            }
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            double lval = left.evaluateReal(t);
            double rval = right.evaluateReal(t);
            switch (tag)
            {
                case NodeTag.opRealAdd: 
                    return lval + rval;
				
                case NodeTag.opRealSub: 
                    return lval - rval;
				
                case NodeTag.opRealMul: 
                    return lval * rval;
				
                case NodeTag.opRealDiv: 
                    return lval / rval;
				
                case NodeTag.opRealPow: 
                    return Math.Pow(lval, rval);
				
                default: 
                    throw new Exception("Invalid tag");
				
            }
        }
		
        internal override string evaluateStr(FilterIterator t)
        {
            string lval = left.evaluateStr(t);
            string rval = right.evaluateStr(t);
            return lval + rval;
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            switch (type) { 
                case NodeType.tpInt:
                    return evaluateInt(t);
                case NodeType.tpReal:
                    return evaluateReal(t);
                case NodeType.tpStr:
                    return evaluateStr(t);
            }
            object lval, rval;
            try
            {
                lval = left.evaluateObj(t);
            }
            catch (JSQLRuntimeException x)
            {
                t.query.ReportRuntimeError(x);
                if (tag == NodeTag.opAnyOr) 
                { 
                    rval = right.evaluateObj(t);  
                    if (rval == null)
                    { 
                        return null;
                    } 
                    if (rval is bool)             
                    {  
                        return (bool)rval;
                    }
                }
                throw x;
            }
            switch (tag)
            {					
               case NodeTag.opAnyAnd: 
                   if (!(lval is bool) || !(bool)lval)
                   {
                       return false;
                   } 
                   break;
                case NodeTag.opAnyOr: 
                   if (lval is bool && (bool)lval) 
                   {
                       return true; 
                   }
                   break;
            }
            rval = right.evaluateObj(t);
            if (lval == null || rval == null) 
            {
                return null;
            }
            if (lval is bool && rval is bool)
            {
                switch (tag)
                {					
                    case NodeTag.opAnyAnd: 
                        return (bool)lval && (bool)rval;
                    case NodeTag.opAnyOr: 
                        return (bool)lval || (bool)rval;
                    default: 
                        throw new Exception("Operation is not applicable to operands of boolean type");
                }
            }            
            if (lval is double || lval is float)
            {
                double r1 = Convert.ToDouble(lval);
                double r2 = Convert.ToDouble(rval);
                switch (tag)
                {				
                    case NodeTag.opAnyAdd: 
                        return r1 + r2;
					
                    case NodeTag.opAnySub: 
                        return r1 - r2;
					
                    case NodeTag.opAnyMul: 
                        return r1 * r2;
					
                    case NodeTag.opAnyDiv: 
                        return r1 / r2;
					
                    case NodeTag.opAnyPow: 
                        return Math.Pow(r1, r2);
					
                    default: 
                        throw new Exception("Operation is not applicable to operands of real type");
					
                }
            }
            else if (lval is string && rval is string)
            {
                return (string) lval + (string) rval;
            }
            else
            {
                long i1 = Convert.ToInt64(lval);
                long i2 = Convert.ToInt64(rval);
                long res;
                switch (tag)
                {
                    case NodeTag.opAnyAdd: 
                        return i1 + i2;
					
                    case NodeTag.opAnySub: 
                        return i1 - i2;
					
                    case NodeTag.opAnyMul: 
                        return i1 * i2;
					
                    case NodeTag.opAnyDiv: 
                        if (i2 == 0)
                        {
                            throw new JSQLArithmeticException("Divided by zero");
                        }
                        return i1 / i2;
					
                    case NodeTag.opAnyAnd: 
                        return i1 & i2;
					
                    case NodeTag.opAnyOr: 
                        return i1 | i2;
					
                    case NodeTag.opAnyPow: 
                        res = 1;
                        if (i1 < 0)
                        {
                            i2 = 1 / i2;
                            i1 = - i1;
                        }
                        while (i1 != 0)
                        {
                            if ((i1 & 1) != 0)
                            {
                                res *= i2;
                            }
                            i2 *= i2;
                            i1 = (long)((ulong)i1 >> 1);
                        }
                        return res;
					
                    default: 
                        throw new Exception("Operation is not applicable to operands of integer type");
					
                }
            }
        }
		
        internal static bool areEqual(object a, object b)
        {
            if (a == null || b == null) 
            {
                return false; 
            }
            if (a == b)
            {
                return true;
            }
            if (a is double || a is float || b is double || b is float)
            {
                return Convert.ToDouble(a) == Convert.ToDouble(b);
            }
#if WINRT_NET_FRAMEWORK
            else if (typeof(long).GetTypeInfo().IsAssignableFrom(a.GetType().GetTypeInfo())) 
#else
            else if (typeof(long).IsAssignableFrom(a.GetType())) 
#endif
            {
                return Convert.ToInt64(a) == Convert.ToInt64(b);
            } 
            else 
            {
                return a.Equals(b);
            }
        }
		
        internal static int compare(object a, object b)
        {
            if (a == null || b == null)
            {
                return Unknown;
            }
            else if (a is double || a is float || b is double || b is float)
            {
                double r1 = Convert.ToDouble(a);
                double r2 = Convert.ToDouble(b);
                return r1 < r2?- 1:r1 == r2?0:1;
            }
            else
            {
#if WINRT_NET_FRAMEWORK
                if (typeof(long).GetTypeInfo().IsAssignableFrom(a.GetType().GetTypeInfo())) 
#else
                if (typeof(long).IsAssignableFrom(a.GetType())) 
#endif
                {
                    long i1 = Convert.ToInt64(a);
                    long i2 = Convert.ToInt64(b);
                    return i1 < i2?- 1:i1 == i2?0:1;
                } 
                else 
                {
                    return ((IComparable) a).CompareTo(b);
                }
            }
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            int diff;
            switch (tag)
            {			
                case NodeTag.opAnyEq: 
                    return areEqual(left.evaluateObj(t), right.evaluateObj(t));
				
                case NodeTag.opAnyNe: 
                    return !areEqual(left.evaluateObj(t), right.evaluateObj(t));
				
                case NodeTag.opAnyLt: 
                    diff = compare(left.evaluateObj(t), right.evaluateObj(t));
                    return diff != Unknown && diff < 0;
				
                case NodeTag.opAnyLe: 
                    diff = compare(left.evaluateObj(t), right.evaluateObj(t));
                    return diff != Unknown && diff <= 0;
				
                case NodeTag.opAnyGt: 
                    diff = compare(left.evaluateObj(t), right.evaluateObj(t));
                    return diff != Unknown && diff > 0;
				
                case NodeTag.opAnyGe: 
                    diff = compare(left.evaluateObj(t), right.evaluateObj(t));
                    return diff != Unknown && diff >= 0;
				
                case NodeTag.opInAny: 
                {
                    object key = left.evaluateObj(t);
                    object set = right.evaluateObj(t);
                    if (set is string)
                    {
                        return ((string) set).IndexOf((string) key) >= 0;
                    }
                    else if (key == null || set == null) 
                    {
                        return false;
                    } 
                    else 
                    {
                        foreach (object elem in (IEnumerable)set)
                        {
                            if (key.Equals(elem))
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                }
				
                case NodeTag.opBoolAnd: 
                    try
                    {
                        if (!left.evaluateBool(t))
                        {
                            return false;
                        }
                    }
                    catch (JSQLRuntimeException x)
                    {
                        t.query.ReportRuntimeError(x); 
                        return false;
                    }
                    return right.evaluateBool(t);
				
                case NodeTag.opBoolOr: 
                    try
                    {
                        if (left.evaluateBool(t))
                        {
                            return true;
                        }
                    }
                    catch (JSQLRuntimeException x)
                    {
                        t.query.ReportRuntimeError(x);
                    }
                    return right.evaluateBool(t);
				
				
                case NodeTag.opIntEq: 
                    return left.evaluateInt(t) == right.evaluateInt(t);
				
                case NodeTag.opIntNe: 
                    return left.evaluateInt(t) != right.evaluateInt(t);
				
                case NodeTag.opIntLt: 
                    return left.evaluateInt(t) < right.evaluateInt(t);
				
                case NodeTag.opIntLe: 
                    return left.evaluateInt(t) <= right.evaluateInt(t);
				
                case NodeTag.opIntGt: 
                    return left.evaluateInt(t) > right.evaluateInt(t);
				
                case NodeTag.opIntGe: 
                    return left.evaluateInt(t) >= right.evaluateInt(t);
				
				
                case NodeTag.opRealEq: 
                    return left.evaluateReal(t) == right.evaluateReal(t);
				
                case NodeTag.opRealNe: 
                    return left.evaluateReal(t) != right.evaluateReal(t);
				
                case NodeTag.opRealLt: 
                    return left.evaluateReal(t) < right.evaluateReal(t);
				
                case NodeTag.opRealLe: 
                    return left.evaluateReal(t) <= right.evaluateReal(t);
				
                case NodeTag.opRealGt: 
                    return left.evaluateReal(t) > right.evaluateReal(t);
				
                case NodeTag.opRealGe: 
                    return left.evaluateReal(t) >= right.evaluateReal(t);
				
				
                case NodeTag.opStrEq: 
                    return left.evaluateStr(t).Equals(right.evaluateStr(t));
				
                case NodeTag.opStrNe: 
                    return !left.evaluateStr(t).Equals(right.evaluateStr(t));
				
                case NodeTag.opStrLt: 
                    return left.evaluateStr(t).CompareTo(right.evaluateStr(t)) < 0;
				
                case NodeTag.opStrLe: 
                    return left.evaluateStr(t).CompareTo(right.evaluateStr(t)) <= 0;
				
                case NodeTag.opStrGt: 
                    return left.evaluateStr(t).CompareTo(right.evaluateStr(t)) > 0;
				
                case NodeTag.opStrGe: 
                    return left.evaluateStr(t).CompareTo(right.evaluateStr(t)) >= 0;
				

                case NodeTag.opStrIgnoreCaseEq: 
                    return String.Compare(left.evaluateStr(t), right.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) == 0;
				
                case NodeTag.opStrIgnoreCaseNe: 
                    return String.Compare(left.evaluateStr(t), right.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) != 0;
				
                case NodeTag.opStrIgnoreCaseLt: 
                    return String.Compare(left.evaluateStr(t), right.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) < 0;
				
                case NodeTag.opStrIgnoreCaseLe: 
                    return String.Compare(left.evaluateStr(t), right.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) <= 0;
				
                case NodeTag.opStrIgnoreCaseGt: 
                    return String.Compare(left.evaluateStr(t), right.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) > 0;
				
                case NodeTag.opStrIgnoreCaseGe: 
                    return String.Compare(left.evaluateStr(t), right.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) >= 0;
				
				
                case NodeTag.opDateEq: 
                    return left.evaluateDate(t).Equals(right.evaluateDate(t));
				
                case NodeTag.opDateNe: 
                    return !left.evaluateDate(t).Equals(right.evaluateDate(t));
				
                case NodeTag.opDateLt: 
                    return left.evaluateDate(t).CompareTo(right.evaluateDate(t)) < 0;
				
                case NodeTag.opDateLe: 
                    return left.evaluateDate(t).CompareTo(right.evaluateDate(t)) <= 0;
				
                case NodeTag.opDateGt: 
                    return left.evaluateDate(t).CompareTo(right.evaluateDate(t)) > 0;
				
                case NodeTag.opDateGe: 
                    return left.evaluateDate(t).CompareTo(right.evaluateDate(t)) >= 0;
				
				
                case NodeTag.opBoolEq: 
                    return left.evaluateBool(t) == right.evaluateBool(t);
				
                case NodeTag.opBoolNe: 
                    return left.evaluateBool(t) != right.evaluateBool(t);
								
                case NodeTag.opObjEq: 
                    return Object.Equals(left.evaluateObj(t), right.evaluateObj(t));
                //    return left.evaluateObj(t) == right.evaluateObj(t);

                case NodeTag.opObjNe: 
                    return !Object.Equals(left.evaluateObj(t), right.evaluateObj(t));
                //    return left.evaluateObj(t) != right.evaluateObj(t);
				
                case NodeTag.opScanCollection: 
                    foreach (object o in (ICollection) right.evaluateObj(t)) 
                    {
                        if (o == left.evaluateObj(t)) 
                        {
                            return true;
                        }
                    }
                    return false;
				
                case NodeTag.opScanArrayBool: 
                {
                    bool val = left.evaluateBool(t);
                    bool[] arr = (bool[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
								
                case NodeTag.opScanArrayChar: 
                {
                    long val = left.evaluateInt(t);
                    char[] arr = (char[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayInt1: 
                {
                    long val = left.evaluateInt(t);
                    sbyte[] arr = (sbyte[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                case NodeTag.opScanArrayInt2: 
                {
                    long val = left.evaluateInt(t);
                    short[] arr = (short[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayInt4: 
                {
                    long val = left.evaluateInt(t);
                    int[] arr = (int[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayInt8: 
                {
                    long val = left.evaluateInt(t);
                    long[] arr = (long[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayUInt1: 
                {
                    long val = left.evaluateInt(t);
                    byte[] arr = (byte[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                case NodeTag.opScanArrayUInt2: 
                {
                    long val = left.evaluateInt(t);
                    ushort[] arr = (ushort[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayUInt4: 
                {
                    long val = left.evaluateInt(t);
                    uint[] arr = (uint[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayUInt8: 
                {
                    long val = left.evaluateInt(t);
                    ulong[] arr = (ulong[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if ((long)arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayReal4: 
                {
                    double val = left.evaluateReal(t);
                    float[] arr = (float[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayReal8: 
                {
                    double val = left.evaluateReal(t);
                    double[] arr = (double[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (arr[i] == val)
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayStr: 
                {
                    string val = left.evaluateStr(t);
                    string[] arr = (string[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (val.Equals(arr[i]))
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opScanArrayObj: 
                {
                    object val = left.evaluateObj(t);
                    object[] arr = (object[]) right.evaluateObj(t);
                    for (int i = arr.Length; --i >= 0; )
                    {
                        if (val == arr[i])
                        {
                            return true;
                        }
                    }
                    return false;
                }
				
                case NodeTag.opInString: 
                {
                    string substr = left.evaluateStr(t);
                    string str = right.evaluateStr(t);
                    return str.IndexOf(substr) >= 0;
                }
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal BinOpNode(NodeType type, NodeTag tag, Node left, Node right):base(type, tag)
        {
            this.left = left;
            this.right = right;
        }
    }
	
    class CompareNode:Node
    {
        internal Node o1, o2, o3;
		
        public  override bool Equals(object o)
        {
            return o is CompareNode 
                && base.Equals(o) 
                && ((CompareNode) o).o1.Equals(o1) 
                && ((CompareNode) o).o2.Equals(o2) 
                && equalObjects(((CompareNode) o).o3, o3);
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            switch (tag)
            {				
                case NodeTag.opAnyBetween: 
                {
                    object val = o1.evaluateObj(t);
                    int diff = BinOpNode.compare(val, o2.evaluateObj(t));
                    if (diff == Unknown || diff < 0) 
                    { 
                        return false;
                    }
                    diff = BinOpNode.compare(val, o3.evaluateObj(t));
                    return diff != Unknown && diff <= 0;
                }
				
                case NodeTag.opIntBetween: 
                {
                    long val = o1.evaluateInt(t);
                    return val >= o2.evaluateInt(t) && val <= o3.evaluateInt(t);
                }
				
                case NodeTag.opRealBetween: 
                {
                    double val = o1.evaluateReal(t);
                    return val >= o2.evaluateReal(t) && val <= o3.evaluateReal(t);
                }
				
                case NodeTag.opStrBetween: 
                {
                    string val = o1.evaluateStr(t);
                    return val.CompareTo(o2.evaluateStr(t)) >= 0 && val.CompareTo(o3.evaluateStr(t)) <= 0;
                }
				
                case NodeTag.opStrIgnoreCaseBetween: 
                {
                    string val = o1.evaluateStr(t);
                    return String.Compare(val, o2.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) >= 0 
                        && String.Compare(val, o3.evaluateStr(t), StringComparison.CurrentCultureIgnoreCase) <= 0;
                }
				
                case NodeTag.opDateBetween: 
                {
                    DateTime val = o1.evaluateDate(t);
                    return val.CompareTo(o2.evaluateDate(t)) >= 0 && val.CompareTo(o3.evaluateDate(t)) <= 0;
                }
				
                case NodeTag.opStrLike: 
                case NodeTag.opStrIgnoreCaseLike: 
                {
                    string str = o1.evaluateStr(t);
                    string pat = o2.evaluateStr(t);
                    if (tag == NodeTag.opStrIgnoreCaseLike) 
                    { 
                        str = str.ToLower();
                        pat = pat.ToLower();
                    }
                    int pi = 0, si = 0, pn = pat.Length, sn = str.Length;
                    int wildcard = - 1, strpos = - 1;
                    while (true)
                    {
                        if (pi < pn && pat[pi] == '%')
                        {
                            wildcard = ++pi;
                            strpos = si;
                        }
                        else if (si == sn)
                        {
                            return pi == pn;
                        }
                        else if (pi < pn && (str[si] == pat[pi] || pat[pi] == '_'))
                        {
                            si += 1;
                            pi += 1;
                        }
                        else if (wildcard >= 0)
                        {
                            si = ++strpos;
                            pi = wildcard;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
				
                case NodeTag.opStrLikeEsc: 
                case NodeTag.opStrIgnoreCaseLikeEsc: 
                {
                    string str = o1.evaluateStr(t);
                    string pat = o2.evaluateStr(t);
                    char escape = o3.evaluateStr(t)[0];
                    if (tag == NodeTag.opStrIgnoreCaseLikeEsc) 
                    { 
                        str = str.ToLower();
                        pat = pat.ToLower();
                    }
                    int pi = 0, si = 0, pn = pat.Length, sn = str.Length;
                    int wildcard = - 1, strpos = - 1;
                    while (true)
                    {
                        if (pi < pn && pat[pi] == '%')
                        {
                            wildcard = ++pi;
                            strpos = si;
                        }
                        else if (si == sn)
                        {
                            return pi == pn;
                        }
                        else if (pi + 1 < pn && pat[pi] == escape && pat[pi + 1] == str[si])
                        {
                            si += 1;
                            pi += 2;
                        }
                        else if (pi < pn && ((pat[pi] != escape && (str[si] == pat[pi] || pat[pi] == '_'))))
                        {
                            si += 1;
                            pi += 1;
                        }
                        else if (wildcard >= 0)
                        {
                            si = ++strpos;
                            pi = wildcard;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal CompareNode(NodeTag tag, Node a, Node b, Node c):base(NodeType.tpBool, tag)
        {
            o1 = a;
            o2 = b;
            o3 = c;
        }
    }
	
	
    class UnaryOpNode:Node
    {
        internal Node opd;
		
        public  override bool Equals(object o)
        {
            return o is UnaryOpNode && base.Equals(o) && ((UnaryOpNode) o).opd.Equals(opd);
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            switch (type) { 
                case NodeType.tpInt:
                    return evaluateInt(t);
                case NodeType.tpReal:
                    return evaluateReal(t);
                case NodeType.tpStr:
                    return evaluateStr(t);
            }
            object val = opd.evaluateObj(t);
            switch (tag)
            {				
                case NodeTag.opAnyNeg: 
                    return val == null ? null : val is double || val is float
                        ? (object)-Convert.ToDouble(val) : (object)-Convert.ToInt64(val);
				
                case NodeTag.opAnyAbs: 
                    if (val == null) 
                    { 
                        return null;
                    }  
                    else if (val is double || val is float)
                    {
                        double rval = Convert.ToDouble(val);
                        return rval < 0?-rval:rval;
                    }
                    else
                    {
                        long ival = Convert.ToInt64(val);
                        return ival < 0?-ival:ival;
                    }
				
                case NodeTag.opAnyNot: 
                    return val == null ? null : val is bool? (bool)val : (object)~Convert.ToInt64(val);
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            long val;
            switch (tag)
            {
                case NodeTag.opIntNot: 
                    return ~opd.evaluateInt(t);
				
                case NodeTag.opIntNeg: 
                    return -opd.evaluateInt(t);
				
                case NodeTag.opIntAbs: 
                    val = opd.evaluateInt(t);
                    return val < 0?-val:val;
				
                case NodeTag.opRealToInt: 
                    return (long) opd.evaluateReal(t);
				
                case NodeTag.opAnyLength: 
                {
                    object obj = opd.evaluateObj(t);
                    if (obj is string)
                    {
                        return ((string) obj).Length;
                    }
                    else
                    {
                        return ((Array) obj).Length;
                    }
                }
				
                case NodeTag.opLength: 
                    return ((Array) opd.evaluateObj(t)).Length;
				
                case NodeTag.opStrLength: 
                    return opd.evaluateStr(t).Length;
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            double val;
            switch (tag)
            {
                case NodeTag.opRealNeg: 
                    return -opd.evaluateReal(t);
				
                case NodeTag.opRealAbs: 
                    val = opd.evaluateReal(t);
                    return val < 0?- val:val;
				
                case NodeTag.opRealSin: 
                    return Math.Sin(opd.evaluateReal(t));
				
                case NodeTag.opRealCos: 
                    return Math.Cos(opd.evaluateReal(t));
				
                case NodeTag.opRealTan: 
                    return Math.Tan(opd.evaluateReal(t));
				
                case NodeTag.opRealAsin: 
                    return Math.Asin(opd.evaluateReal(t));
				
                case NodeTag.opRealAcos: 
                    return Math.Acos(opd.evaluateReal(t));
				
                case NodeTag.opRealAtan: 
                    return Math.Atan(opd.evaluateReal(t));
				
                case NodeTag.opRealExp: 
                    return Math.Exp(opd.evaluateReal(t));
				
                case NodeTag.opRealLog: 
                    return Math.Log(opd.evaluateReal(t));
				
                case NodeTag.opRealSqrt: 
                    return Math.Sqrt(opd.evaluateReal(t));
				
                case NodeTag.opRealCeil: 
                    return Math.Ceiling(opd.evaluateReal(t));
				
                case NodeTag.opRealFloor: 
                    return Math.Floor(opd.evaluateReal(t));
				
                case NodeTag.opIntToReal: 
                    return (double) opd.evaluateInt(t);
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal override DateTime evaluateDate(FilterIterator t)
        {
            switch (tag)
            {				
                case NodeTag.opStrToDate:             
                    return DateTime.Parse(opd.evaluateStr(t));
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
				
        internal override string evaluateStr(FilterIterator t)
        {
            switch (tag)
            {				
                case NodeTag.opStrUpper: 
                    return opd.evaluateStr(t).ToUpper();
				
                case NodeTag.opStrLower: 
                    return opd.evaluateStr(t).ToLower();
				
                case NodeTag.opIntToStr: 
                    return System.Convert.ToString(opd.evaluateInt(t), 10);
				
                case NodeTag.opRealToStr: 
                    return opd.evaluateReal(t).ToString();

                case NodeTag.opDateToStr: 
                    return opd.evaluateDate(t).ToString();
				
                case NodeTag.opAnyToStr: 
                    return opd.evaluateObj(t).ToString();
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            switch (tag)
            {
                case NodeTag.opBoolNot: 
                    return !opd.evaluateBool(t);
				
                case NodeTag.opIsNull: 
                    return opd.evaluateObj(t) == null;
				
                default: 
                    throw new Exception("Invalid tag " + tag);
				
            }
        }
		
        internal UnaryOpNode(NodeType type, NodeTag tag, Node node):base(type, tag)
        {
            opd = node;
        }
    }
	
	
    class LoadAnyNode:Node
    {
        override internal Type Type
        {
            get
            {
                return typeof(object);
            }
			
        }

        override internal FieldInfo Field
        {
            get
            {
                return f;
            }			
        }

        override internal string FieldName
        {
            get
            {
                if (baseExpr != null)
                {
                    if (baseExpr.tag != NodeTag.opCurrent)
                    {
                        string baseName = baseExpr.FieldName;
                        return (baseName != null)?baseName + "." + fieldName:null;
                    }
                    else
                    {
                        return fieldName;
                    }
                }
                else
                {
                    return containsFieldName != null?containsFieldName + "." + fieldName:fieldName;
                }
            }
			
        }
        internal string fieldName;
        internal string containsFieldName;
        internal Node baseExpr;
        internal FieldInfo f;
        internal MethodInfo m;
		
        public  override bool Equals(object o)
        {
            if (!(o is LoadAnyNode))
            {
                return false;
            }
            LoadAnyNode node = (LoadAnyNode) o;
            return equalObjects(node.baseExpr, baseExpr) 
                && equalObjects(node.fieldName, fieldName) 
                && equalObjects(node.f, f) 
                && equalObjects(node.m, m);
        }
		
		
		
        internal LoadAnyNode(Node baseExpr, string name, string containsFieldName):base(NodeType.tpAny, NodeTag.opLoadAny)
        {
            fieldName = name;
            this.containsFieldName = containsFieldName;
            this.baseExpr = baseExpr;
        }
		
        public override string ToString()
        {
            return "LoadAnyNode: fieldName='" + fieldName + "', containsFieldName='" + containsFieldName + "', base=(" + baseExpr + "), f=" + f + ", m=" + m;
        }
		
		
        internal override object evaluateObj(FilterIterator t)
        {
            object obj;
            Type cls;
            FieldInfo f = this.f;
            MethodInfo m = this.m;
            if (baseExpr == null)
            {
                if (t.containsElem != null)
                {
                    obj = t.containsElem;
                    cls = obj.GetType();
                    if (f != null && f.DeclaringType.Equals(cls))
                    {
                        return t.query.resolve(f.GetValue(obj));
                    }
                    if (m != null && m.DeclaringType.Equals(cls))
                    {
                        return t.query.resolve(m.Invoke(obj, null));
                    }
                    if ((f = QueryImpl.lookupField(cls, fieldName)) != null)
                    {
                        this.f = f;
                        return t.query.resolve(f.GetValue(obj));
                    }
                    if ((m = QueryImpl.lookupMethod(cls, fieldName, QueryImpl.defaultProfile)) != null)
                    {
                        this.m = m;
                        return t.query.resolve(m.Invoke(obj, null));
                    }
                }
                obj = t.currObj;
            }
            else
            {
                obj = baseExpr.evaluateObj(t);
                if (obj == null)
                {
                    throw new JSQLNullPointerException(null, fieldName);
                }
            }
            cls = obj.GetType();
            if (f != null && f.DeclaringType.Equals(cls))
            {
                return t.query.resolve(f.GetValue(obj));
            }
            if (m != null && m.DeclaringType.Equals(cls))
            {
                return t.query.resolve(m.Invoke(obj, null));
            }
            if ((f = QueryImpl.lookupField(cls, fieldName)) != null)
            {
                this.f = f;
                return t.query.resolve(f.GetValue(obj));
            }
            if ((m = QueryImpl.lookupMethod(cls, fieldName, QueryImpl.defaultProfile)) != null)
            {
                this.m = m;
                return t.query.resolve(m.Invoke(obj, null));
            }			
            throw new JSQLNoSuchFieldException(cls, fieldName);
        }
    }
	
    class ResolveNode:Node
    {
        override internal Type Type
        {
            get
            {
                return resolvedClass;
            }			
        }

        override internal FieldInfo Field
        {
            get
            {
                return (expr != null) ?  expr.Field : null;
            }			
        }

        override internal string FieldName
        {
            get
            {
                return (expr != null) ? expr.FieldName : null;
            }		
        }

        internal Resolver resolver;
        internal Type resolvedClass;
        internal Node expr;
		
        public  override bool Equals(object o)
        {
            return o is ResolveNode 
                && ((ResolveNode) o).expr.Equals(expr) 
                && ((ResolveNode) o).resolver.Equals(resolver) 
                && ((ResolveNode) o).resolvedClass.Equals(resolvedClass);
        }
		
		
        internal override object evaluateObj(FilterIterator t)
        {
            return resolver.Resolve(expr.evaluateObj(t));
        }
		
		
        internal ResolveNode(Node expr, Resolver resolver, Type resolvedClass):base(NodeType.tpObj, NodeTag.opResolve)
        {
            this.expr = expr;
            this.resolver = resolver;
            this.resolvedClass = resolvedClass;
        }
    }
	
    class LoadNode:Node
    {
        internal bool IsSelfField
        {
            get
            { 
                return baseExpr == null || baseExpr.tag == NodeTag.opCurrent;
            }
        }

        internal Type DeclaringType
        {
            get
            {
                return field.DeclaringType;
            }			
        }

        override internal Type Type
        {
            get
            {
                return field.FieldType;
            }			
        }

        override internal FieldInfo Field
        {
            get
            {
                return field;
            }			
        }

        override internal string FieldName
        {
            get
            {
                if (baseExpr != null && baseExpr.tag != NodeTag.opCurrent)
                {
                    string baseName = baseExpr.FieldName;
                    return (baseName != null)?baseName + "." + field.Name:null;
                }
                else
                {
                    return field.Name;
                }
            }		
        }

        override internal int IndirectionLevel
        {
            get
            {
                return baseExpr == null ? 0 : baseExpr.IndirectionLevel + 1;
            }
        }

        internal FieldInfo field;
        internal Node baseExpr;
		
        public  override bool Equals(object o)
        {
            return o is LoadNode && base.Equals(o) 
                && ((LoadNode) o).field.Equals(field) 
                && equalObjects(((LoadNode) o).baseExpr, baseExpr);
        }
		
		
		
        internal object getBase(FilterIterator t)
        {
            if (baseExpr == null)
            {
                return t.currObj;
            }
            object obj = baseExpr.evaluateObj(t);
            if (obj == null)
            {
                throw new JSQLNullPointerException(baseExpr.Type, field.Name);
            }
            return obj;
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            return Convert.ToInt64(field.GetValue(getBase(t)));
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            return Convert.ToDouble(field.GetValue(getBase(t)));
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            return (bool) field.GetValue(getBase(t));
        }
		
        internal override string evaluateStr(FilterIterator t)
        {
            return wrapNullString(field.GetValue(getBase(t)));
        }
		
        internal override object evaluateObj(FilterIterator t)
        {
            return field.GetValue(getBase(t));
        }
		
        internal LoadNode(Node baseExpr, FieldInfo f):base(getNodeType(f.FieldType), NodeTag.opLoad)
        {
            field = f;
            this.baseExpr = baseExpr;
        }
    }
	
	
    class AggregateFunctionNode:Node
    {
        public  override bool Equals(object o)
        {
            return o is AggregateFunctionNode 
                && base.Equals(o) 
                && equalObjects(((AggregateFunctionNode) o).argument, argument) 
                && ((AggregateFunctionNode) o).index == index;
        }
		
        internal override long evaluateInt(FilterIterator t)
        {
            return t.intAggragateFuncValue[index];
        }
		
        internal override double evaluateReal(FilterIterator t)
        {
            return t.realAggragateFuncValue[index];
        }
		
        internal AggregateFunctionNode(NodeType type, NodeTag tag, Node arg):base(type, tag)
        {
            argument = arg;
        }
		
        internal int  index;
        internal Node argument;
    }
	
    class InvokeElementNode:InvokeNode
    {
        override internal string FieldName
        {
            get
            {
                if (containsArrayName != null)
                {
                    return containsArrayName + "." + mth.Name;
                }
                else
                {
                    return null;
                }
            }
			
        }
        internal string containsArrayName;
		
        internal InvokeElementNode(MethodInfo mth, Node[] arguments, string arrayName):base(null, mth, arguments)
        {
            containsArrayName = arrayName;
        }
		
        public  override bool Equals(object o)
        {
            return o is InvokeElementNode && base.Equals(o);
        }
		
        internal override object getTarget(FilterIterator t)
        {
            return t.containsElem;
        }
		
    }
	
    class ElementNode:Node
    {
        override internal FieldInfo Field
        {
            get
            {
                return field;
            }			
        }

        override internal string FieldName
        {
            get
            {
                return arrayName != null?arrayName + "." + field.Name:null;
            }
			
        }
        override internal Type Type
        {
            get
            {
                return type;
            }
			
        }
        internal string arrayName;
        internal FieldInfo field;
        new internal Type type;
		
        public  override bool Equals(object o)
        {
            return o is ElementNode 
                && equalObjects(((ElementNode) o).arrayName, arrayName) 
                && equalObjects(((ElementNode) o).field, field) 
                && equalObjects(((ElementNode) o).type, type);
        }
		
        internal ElementNode(string array, FieldInfo f):base(getNodeType(f.FieldType), NodeTag.opElement)
        {
            arrayName = array;
            type = f.FieldType;
            field = f;
        }
		
		
        internal override bool evaluateBool(FilterIterator t)
        {
            return (bool) field.GetValue(t.containsElem);
        }

        internal override long evaluateInt(FilterIterator t)
        {
            return (long) field.GetValue(t.containsElem);
        }

        internal override double evaluateReal(FilterIterator t)
        {
            return (double) field.GetValue(t.containsElem);
        }

        internal override string evaluateStr(FilterIterator t)
        {
            return wrapNullString(field.GetValue(t.containsElem));
        }

        internal override object evaluateObj(FilterIterator t)
        {
            return field.GetValue(t.containsElem);
        }
    }
	
    class ContainsNode:Node, IComparer
    {
        internal Node       containsExpr;
        internal FieldInfo  groupByField;
        internal MethodInfo groupByMethod;
        internal string     groupByFieldName;
        internal Type       containsFieldClass;
        internal NodeType   groupByType;
        internal Node       havingExpr;
        internal Node       withExpr;
        internal Resolver   resolver;
        internal ArrayList  aggregateFunctions;
		
        public  override bool Equals(object o)
        {
            if (!(o is ContainsNode))
            {
                return false;
            }
            ContainsNode node = (ContainsNode) o;
            return node.containsExpr.Equals(containsExpr) 
                && equalObjects(node.groupByField, groupByField) 
                && equalObjects(node.groupByMethod, groupByMethod) 
                && equalObjects(node.groupByFieldName, groupByFieldName)
                && equalObjects(node.containsFieldClass, containsFieldClass) 
                && node.groupByType == groupByType 
                && equalObjects(node.havingExpr, havingExpr) 
                && equalObjects(node.withExpr, withExpr) 
                && equalObjects(node.aggregateFunctions, aggregateFunctions);
        }
		
        public int Compare(object o1, object o2)
        {
            if (o1 == o2)
            {
                return 0;
            }
            if (groupByMethod != null)
            {
                return ((IComparable) groupByMethod.Invoke(o1, null)).CompareTo(groupByMethod.Invoke(o2, null));
            }
            switch (groupByType)
            {					
                case NodeType.tpInt: 
                {
                    long v1 = (long) groupByField.GetValue(o1);
                    long v2 = (long) groupByField.GetValue(o2);
                    return v1 < v2?-1:v1 == v2?0:1;
                }
					
                case NodeType.tpReal: 
                {
                    double v1 = (double) groupByField.GetValue(o1);
                    double v2 = (double) groupByField.GetValue(o2);
                    return v1 < v2?- 1:v1 == v2?0:1;
                }
					
                case NodeType.tpBool: 
                {
                    bool v1 = (bool) groupByField.GetValue(o1);
                    bool v2 = (bool) groupByField.GetValue(o2);
                    return v1?(v2?0:1):(v2?- 1:0);
                }
					
                default: 
                {
                    object v1 = groupByField.GetValue(o1); 
                    object v2 = groupByField.GetValue(o2);
                    return v1 == null || v2 == null ? Unknown : ((IComparable)v1).CompareTo(v2);
                }					
            }
        }
		
		
        internal override bool evaluateBool(FilterIterator t)
        {
            int i, j, k, l, n = 0, len = 0;
            object collection;
            collection = containsExpr.evaluateObj(t);
            if (collection == null)
            {
                return false;
            }
            object[] sortedArray = null;
            if (havingExpr != null && (withExpr != null || !(collection is ICollection)))
            {
                n = (collection is ICollection)?((ICollection) collection).Count:((object[]) collection).Length;
                if (t.containsArray == null || t.containsArray.Length < n)
                {
                    t.containsArray = new object[n];
                }
                sortedArray = t.containsArray;
                t.containsArray = null; // prevent reuse of the same array by nexted CONTAINS in with expression
            }
            object saveContainsElem = t.containsElem;
			
            if (collection is ICollection)
            {
                if (withExpr != null)
                {
                    foreach (object e in (ICollection)collection)
                    {
                        object elem = e;
                        if (elem != null)
                        {
                            if (resolver != null)
                            {
                                elem = resolver.Resolve(elem);
                            }
                            else
                            {
                                elem = t.query.resolve(elem);
                            }
                            t.containsElem = elem;
                            try
                            {
                                if (withExpr.evaluateBool(t))
                                {
                                    if (havingExpr == null)
                                    {
                                        t.containsElem = saveContainsElem;
                                        return true;
                                    }
                                    sortedArray[len++] = elem;
                                }
                            }
                            catch (JSQLRuntimeException x)
                            {
                                t.query.ReportRuntimeError(x);
                            }
                        }
                    }
                }
                else
                {
                    sortedArray = new object[((ICollection) collection).Count];
                    ((ICollection)collection).CopyTo(sortedArray, 0);
                    n = sortedArray.Length;
                    if (t.query.resolveMap != null)
                    {
                        for (i = 0; i < n; i++)
                        {
                            sortedArray[i] = t.query.resolve(sortedArray[i]);
                        }
                    }
                    len = n;
                }
            }
            else
            {
                object[] a = (object[]) collection;
                n = a.Length;
                if (withExpr != null)
                {
                    for (i = 0; i < n; i++)
                    {
                        object elem = a[i];
                        if (elem != null)
                        {
                            if (resolver != null)
                            {
                                elem = resolver.Resolve(elem);
                            }
                            else
                            {
                                elem = t.query.resolve(elem);
                            }
                            t.containsElem = elem;
                            try
                            {
                                if (withExpr.evaluateBool(t))
                                {
                                    if (havingExpr == null)
                                    {
                                        t.containsElem = saveContainsElem;
                                        return true;
                                    }
                                    sortedArray[len++] = elem;
                                }
                            }
                            catch (JSQLRuntimeException x)
                            {
                                t.query.ReportRuntimeError(x);
                            }
                        }
                    }
                }
                else
                {
                    Array.Copy(a, 0, sortedArray, 0, n);
                    if (t.query.resolveMap != null)
                    {
                        for (i = 0; i < n; i++)
                        {
                            sortedArray[i] = t.query.resolve(sortedArray[i]);
                        }
                    }
                    len = n;
                }
            }
            t.containsElem = saveContainsElem;
            if (sortedArray != null)
            {
                t.containsArray = sortedArray;
            }
            if (len == 0)
            {
                return false;
            }
            if (groupByFieldName != null && len > 0)
            {
                Type type = sortedArray[0].GetType();
                groupByField = QueryImpl.lookupField(type, groupByFieldName);
                if (groupByField == null)
                {
                    groupByMethod = QueryImpl.lookupMethod(type, groupByFieldName, QueryImpl.defaultProfile);
                    if (groupByMethod == null)
                    {
                        throw new JSQLNoSuchFieldException(type, groupByFieldName);
                    }
                }
                else
                {
                    groupByType = Node.getNodeType(groupByField.FieldType);
                }
            }
            Array.Sort(sortedArray, 0, len, this);
			
            n = aggregateFunctions.Count;
            if (t.intAggragateFuncValue == null || t.intAggragateFuncValue.Length < n)
            {
                t.intAggragateFuncValue = new long[n];
                t.realAggragateFuncValue = new double[n];
            }
            for (i = 0; i < len; i = j)
            {
                for (j = i + 1; j < len && Compare(sortedArray[i], sortedArray[j]) == 0; j++)
                {
                }
                for (k = 0; k < n; k++)
                {
                    AggregateFunctionNode agr = (AggregateFunctionNode)aggregateFunctions[k];
                    Node argument = agr.argument;
                    if (agr.type == NodeType.tpInt)
                    {
                        long ival = 0;
                        switch (agr.tag)
                        {							
                            case NodeTag.opSum: 
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    ival += argument.evaluateInt(t);
                                }
                                break;
							
                            case NodeTag.opAvg: 
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    ival += argument.evaluateInt(t);
                                }
                                ival /= j - i;
                                break;
							
                            case NodeTag.opMin: 
                                ival = System.Int64.MaxValue;
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    long v = argument.evaluateInt(t);
                                    if (v < ival)
                                    {
                                        ival = v;
                                    }
                                }
                                break;
							
                            case NodeTag.opMax: 
                                ival = System.Int64.MinValue;
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    long v = argument.evaluateInt(t);
                                    if (v > ival)
                                    {
                                        ival = v;
                                    }
                                }
                                break;
							
                            case NodeTag.opCount: 
                                ival = j - i;
                                break;
                        }
                        t.intAggragateFuncValue[k] = ival;
                    }
                    else
                    {
                        double rval = 0.0;
						
                        switch (agr.tag)
                        {						
                            case NodeTag.opSum: 
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    rval += argument.evaluateReal(t);
                                }
                                break;
							
                            case NodeTag.opAvg: 
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    rval += argument.evaluateReal(t);
                                }
                                rval /= j - i;
                                break;
							
                            case NodeTag.opMin: 
                                rval = double.PositiveInfinity;
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    double v = argument.evaluateReal(t);
                                    if (v < rval)
                                    {
                                        rval = v;
                                    }
                                }
                                break;
							
                            case NodeTag.opMax: 
                                rval = double.NegativeInfinity;
                                for (l = i; l < j; l++)
                                {
                                    t.containsElem = sortedArray[l];
                                    double v = argument.evaluateReal(t);
                                    if (v > rval)
                                    {
                                        rval = v;
                                    }
                                }
                                break;
                        }
                        t.realAggragateFuncValue[k] = rval;
                    }
                }
                t.containsElem = saveContainsElem;
                try
                {
                    if (havingExpr.evaluateBool(t))
                    {
                        return true;
                    }
                }
                catch (JSQLRuntimeException x)
                {
                    t.query.ReportRuntimeError(x);
                }
            }
            return false;
        }
		
		
        internal ContainsNode(Node containsExpr, Type containsFieldClass):base(NodeType.tpBool, NodeTag.opContains)
        {
            this.containsExpr = containsExpr;
            this.containsFieldClass = containsFieldClass;
            aggregateFunctions = new ArrayList();
        }
    }
	
	
    class OrderNode
    {
        internal OrderNode  next;
        internal bool       ascent;
        internal FieldInfo  field;
        internal MethodInfo method;
        internal string     fieldName;
        internal ClassDescriptor.FieldType type;
	    internal Node       expr;
        internal StringComparison culture;

        internal int compare(object a, object b)
        {
            int diff;
            if (method != null)
            {
                diff = ((IComparable) method.Invoke(a, null)).CompareTo(method.Invoke(b, null));
            }
            else if (expr != null) 
            { 
                FilterIterator i1 = new FilterIterator(a);
                FilterIterator i2 = new FilterIterator(b);
                switch (expr.type) 
                { 
                    case NodeType.tpInt:
                    {
                        long l = expr.evaluateInt(i1);
                        long r = expr.evaluateInt(i2);
                        diff = l < r ? -1 : l == r ? 0 : 1;
                        break;
                    }
                    case NodeType.tpReal:
                    {
                        double l = expr.evaluateReal(i1);
                        double r = expr.evaluateReal(i2);
                        diff = l < r ? -1 : l == r ? 0 : 1;
                        break;
                    }
                    case NodeType.tpStr:
                    {
                        string l = expr.evaluateStr(i1);
                        string r = expr.evaluateStr(i2);
                        diff = l.CompareTo(r);
                        break;
                    }
                    case NodeType.tpDate:
                    {
                        DateTime l = expr.evaluateDate(i1);
                        DateTime r = expr.evaluateDate(i2);
                        diff = l.CompareTo(r);
                        break;
                    }
                    default:
                    {
                        IComparable l = (IComparable)expr.evaluateObj(i1);
                        IComparable r = (IComparable)expr.evaluateObj(i2);
                        diff = l.CompareTo(r);
                        break;
                    }
                }
            }                    
            else
            {
                switch (type)
                {						
#if NET_FRAMEWORK_20
                    case ClassDescriptor.FieldType.tpNullableBoolean: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) 
                            ? Node.Unknown 
                            : (bool)v1 ? (bool)v2 ? 0 : 1 : (bool)v2 ? -1 : 0;
                        break;
		    }		
                    case ClassDescriptor.FieldType.tpNullableChar: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (char) v1 - (char) v2;
                        break;
		    }				
                    case ClassDescriptor.FieldType.tpNullableSByte: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (sbyte) v1 - (sbyte) v2;
                        break;
		    }				
                    case ClassDescriptor.FieldType.tpNullableShort: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (short) v1 - (short) v2;
                        break;
		    }				
                    case ClassDescriptor.FieldType.tpNullableInt: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (int)v1 < (int)v2?- 1:(int)v1 == (int)v2?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpNullableLong: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (long)v1 < (long)v2?- 1:(long)v1 == (long)v2?0:1;
                        break;
                    }

                    case ClassDescriptor.FieldType.tpNullableByte: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (byte) v1 - (byte) v2;
                        break;
		    }		
                    case ClassDescriptor.FieldType.tpNullableUShort: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (ushort) v1 - (ushort) v2;
                        break;
	            }			
                    case ClassDescriptor.FieldType.tpNullableUInt: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (uint)v1 < (uint)v2?- 1:(uint)v1 == (uint)v2?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpNullableULong: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (ulong)v1 < (ulong)v2?- 1:(ulong)v1 == (ulong)v2?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpNullableFloat: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (float)v1 < (float)v2?- 1:(float)v1 == (float)v2?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpNullableDouble: 
                    {
                        object v1 = field.GetValue(a);
                        object v2 = field.GetValue(b);
                        diff = (v1 == null || v2 == null) ? Node.Unknown : (double)v1 < (double)v2?- 1:(double)v1 == (double)v2?0:1;
                        break;
                    }
#endif						

                    case ClassDescriptor.FieldType.tpBoolean: 
                        diff = (bool) field.GetValue(a) ?(bool) field.GetValue(b)?0:1:(bool) field.GetValue(b)?-1:0;
                        break;
						
                    case ClassDescriptor.FieldType.tpChar: 
                        diff = (char) field.GetValue(a) - (char) field.GetValue(b);
                        break;
						
                    case ClassDescriptor.FieldType.tpSByte: 
                        diff = (sbyte) field.GetValue(a) - (sbyte) field.GetValue(b);
                        break;
						
                    case ClassDescriptor.FieldType.tpShort: 
                        diff = (short) field.GetValue(a) - (short) field.GetValue(b);
                        break;
						
                    case ClassDescriptor.FieldType.tpInt: 
                    {
                        int l = (int) field.GetValue(a);
                        int r = (int) field.GetValue(b);
                        diff = l < r?- 1:l == r?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpLong: 
                    {
                        long l = (long) field.GetValue(a);
                        long r = (long) field.GetValue(b);
                        diff = l < r?- 1:l == r?0:1;
                        break;
                    }

                    case ClassDescriptor.FieldType.tpByte: 
                        diff = (byte) field.GetValue(a) - (byte) field.GetValue(b);
                        break;
						
                    case ClassDescriptor.FieldType.tpUShort: 
                        diff = (ushort) field.GetValue(a) - (ushort) field.GetValue(b);
                        break;
						
                    case ClassDescriptor.FieldType.tpUInt: 
                    {
                        uint l = (uint) field.GetValue(a);
                        uint r = (uint) field.GetValue(b);
                        diff = l < r?- 1:l == r?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpULong: 
                    {
                        ulong l = (ulong) field.GetValue(a);
                        ulong r = (ulong) field.GetValue(b);
                        diff = l < r?- 1:l == r?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpFloat: 
                    {
                        float l = (float) field.GetValue(a);
                        float r = (float) field.GetValue(b);
                        diff = l < r?- 1:l == r?0:1;
                        break;
                    }
						
                    case ClassDescriptor.FieldType.tpDouble: 
                    {
                        double l = (double) field.GetValue(a);
                        double r = (double) field.GetValue(b);
                        diff = l < r?- 1:l == r?0:1;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpString:
                        diff = String.Compare((String)field.GetValue(a), (String)field.GetValue(b), culture);
                        break;
                    default: 
                        diff = ((IComparable) field.GetValue(a)).CompareTo(field.GetValue(b));
                        break;
						
                }
            }
            if (diff == 0 && next != null)
            {
                return next.compare(a, b);
            }
            if (!ascent)
            {
                diff = - diff;
            }
            return diff;
        }
        		
        void setStringCulture() 
        {
            culture = StringComparison.CurrentCulture;
            if (field != null)
            {
                foreach (IndexableAttribute idx in field.GetCustomAttributes(typeof(IndexableAttribute), false))                
                { 
                    culture = idx.CaseInsensitive ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
                    break;
                }                   
            }
        }

        internal virtual void  resolveName(Type cls)
        {
            field = QueryImpl.lookupField(cls, fieldName);
            if (field == null)
            {
                method = QueryImpl.lookupMethod(cls, fieldName, QueryImpl.defaultProfile);
                if (method == null)
                {
                    throw new JSQLNoSuchFieldException(cls, fieldName);
                }
            }
            else
            {
                setStringCulture();
            }
        }

        internal string Name
        {
            get
            {
                return fieldName != null ? fieldName : field != null ? field.Name : method.Name;
            }
        }
           		
        internal OrderNode(Node expr) 
        { 
            this.expr = expr;
        }

        internal OrderNode(FieldInfo field)
        {
            this.type = ClassDescriptor.getTypeCode(field.FieldType);
            this.field = field;
            setStringCulture();
            ascent = true;
        }
        internal OrderNode(MethodInfo method)
        {
            this.method = method;
            ascent = true;
        }
        internal OrderNode(string name)
        {
            fieldName = name;
            ascent = true;
        }
    }
	
    class ParameterNode:LiteralNode
    {
        override internal object Value
        {
            get
            {
                return parameters[index];
            }
			
        }
        internal ArrayList parameters;
        internal int index;
		
		
        public  override bool Equals(object o)
        {
            return o is ParameterNode && ((ParameterNode) o).index == index;
        }
		
        internal override bool evaluateBool(FilterIterator t)
        {
            return (bool)parameters[index];
        }
        internal override long evaluateInt(FilterIterator t)
        {
            return Convert.ToInt64(parameters[index]);
        }
        internal override double evaluateReal(FilterIterator t)
        {
            return Convert.ToDouble(parameters[index]);
        }
        internal override string evaluateStr(FilterIterator t)
        {
            return (string)parameters[index];
        }
			
        internal override DateTime evaluateDate(FilterIterator t)
        {
            return (DateTime)parameters[index];
        }

        internal ParameterNode(ArrayList parameterList, int index, NodeType type)  
        : base(type, NodeTag.opParameter)
        {
            parameters = parameterList;
            this.index = index;
            while (index >= parameterList.Count) { 
                parameterList.Add(null);
            }
        }

        internal ParameterNode(ArrayList parameterList):base(NodeType.tpUnknown, NodeTag.opParameter)
        {
            parameters = parameterList;
            index = parameters.Count;
            parameters.Add(null);
        }
    }
	
	
    class Symbol
    {
        internal Token tkn;
		
        internal Symbol(Token tkn)
        {
            this.tkn = tkn;
        }
    }
	
	
    class Binding
    {
        internal Binding next;
        internal string name;
        internal bool used;
        internal int loopId;
		
        internal Binding(string ident, int loop, Binding chain)
        {
            name = ident;
            used = false;
            next = chain;
            loopId = loop;
        }
    }
	
#if USE_GENERICS
    public class QueryImpl
#else
    public class QueryImpl : Query
#endif
    {
        internal int pos;
        internal char[] buf;
        internal char[] str;
        internal string query;
        internal long ivalue;
        internal string svalue;
        internal double fvalue;
        internal Type cls;
        internal Node tree;
        internal string ident;
        internal Token lex;
        internal int vars;
        internal Binding bindings;
        internal OrderNode order;
        internal ContainsNode contains;
        internal Node singleElementList;
        internal ArrayList parameters;
        internal bool runtimeErrorsReporting;
        internal Hashtable resolveMap;
        internal IndexProvider indexProvider;
        internal StorageImpl storage;		
        internal static Hashtable symtab;
        internal static Type[] defaultProfile;
        internal static Node[] noArguments;
		
        internal static readonly object dummyKeyValue = new object();
		
        public virtual object this[int index]
        {
            set 
            { 
                parameters[index - 1] = value;
            }
            get 
            { 
                return parameters[index - 1];
            }
        }
 
        public virtual void ReportRuntimeError(JSQLRuntimeException x)
        {
            if (runtimeErrorsReporting)
            {
                System.Text.StringBuilder buf = new System.Text.StringBuilder();
                buf.Append(x.Message);
                Type cls = x.Target;
                if (cls != null)
                {
                    buf.Append(cls.FullName);
                    buf.Append('.');
                }
                string fieldName = x.FieldName;
                if (fieldName != null)
                {
                    buf.Append(fieldName);
                }
#if !WINRT_NET_FRAMEWORK
                Console.WriteLine(buf);
#endif
            }
            if (storage != null && storage.listener != null)
            {
                storage.listener.JSQLRuntimeError(x);
            }
        }
		
        internal static Node int2real(Node expr)
        {
            if (expr.tag == NodeTag.opIntConst)
            {
                return new RealLiteralNode((double) ((IntLiteralNode) expr).val);
            }
            return new UnaryOpNode(NodeType.tpReal, NodeTag.opIntToReal, expr);
        }
		
        internal static Node str2date(Node expr)
        {
            if (expr.tag == NodeTag.opStrConst)
            {
                return new DateLiteralNode(DateTime.Parse(((StrLiteralNode) expr).val));
            }
            return new UnaryOpNode(NodeType.tpDate, NodeTag.opStrToDate, expr);
        }

        public virtual void EnableRuntimeErrorReporting(bool enabled)
        {
            runtimeErrorsReporting = enabled;
        }
		
        internal class ResolveMapping
        {
            internal Type resolved;
            internal Resolver resolver;
			
            internal ResolveMapping(Type resolved, Resolver resolver)
            {
                this.resolved = resolved;
                this.resolver = resolver;
            }
        }
				
        public virtual void  SetResolver(Type original, Type resolved, Resolver resolver)
        {
            if (resolveMap == null)
            {
                resolveMap = new Hashtable();
            }
            resolveMap[original] = new ResolveMapping(resolved, resolver);
        }
		
        public virtual void SetIndexProvider(IndexProvider indexProvider) 
        {
            this.indexProvider = indexProvider;
        }

        internal static Key keyLiteral(Type type, Node node, bool inclusive)
        {
            try
            {
                return new Key(((LiteralNode)node).Value, type, inclusive);
            } 
            catch (InvalidCastException) 
            {
                return null; 
            }
        }

        internal static FieldInfo lookupField(Type cls, string ident)
        {
#if WINRT_NET_FRAMEWORK
            return cls.GetTypeInfo().GetDeclaredField(ident);
#else
            return cls.GetField(ident, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
#endif
        }
		
        internal static MethodInfo lookupMethod(Type cls, string ident, Type[] profile)
        {
#if WINRT_NET_FRAMEWORK
            MethodInfo m = cls.GetRuntimeMethod(ident, profile);
            if (m == null)
            {
                m = cls.GetTypeInfo().GetDeclaredMethod(ident);
                if (m == null && profile.Length == 0)
                {
                    PropertyInfo prop = cls.GetTypeInfo().GetDeclaredProperty(ident);
                    if (prop != null)
                    {
                        m = prop.GetMethod;
                    }
                }
            }
#else
            MethodInfo m = cls.GetMethod(ident, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public, null, profile, null);
            if (m == null && profile.Length == 0)
            {
                PropertyInfo prop = cls.GetProperty(ident, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                if (prop != null) 
                {
                    m = prop.GetGetMethod();
                } 
            }
#endif
            return m;
        }
		
        internal object resolve(object obj)
        {
            if (resolveMap != null)
            {
                ResolveMapping rm = (ResolveMapping) resolveMap[obj.GetType()];
                if (rm != null)
                {
                    obj = rm.resolver.Resolve(obj);
                }
            }
            return obj;
        }

#if USE_GENERICS
    }
    public class QueryImpl<T> : QueryImpl, Query<T>
    {
        internal Dictionary<string,GenericIndex> indices;
#else
        internal Hashtable indices;
#endif

#if USE_GENERICS
        IEnumerable<T> classExtent;
#else
        IEnumerable    classExtent;
#endif
        ClassExtentLockType classExtentLock;
 		
#if USE_GENERICS
        public virtual void SetClassExtent(IEnumerable<T> set, ClassExtentLockType lockType)
#else
        public virtual void SetClassExtent(IEnumerable set, ClassExtentLockType lockType)
#endif
        {
            classExtent = set;
            classExtentLock = lockType;
        }

#if NET_FRAMEWORK_35 && !USE_GENERICS
        class SelectionEnumerable<T> : IEnumerable<T>, IEnumerable
        {
            IEnumerable enumerable;

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new SelectionEnumerator<T>(enumerable.GetEnumerator());
            }

            public IEnumerator<T> GetEnumerator() 
            {
                return new SelectionEnumerator<T>(enumerable.GetEnumerator());
            }

            public SelectionEnumerable(IEnumerable e) 
            {
                enumerable = e;
            }                                                           
         }
          
        class SelectionEnumerator<T> : IEnumerator<T>, IEnumerator
        {
            IEnumerator enumerator;

            public bool MoveNext() 
            {
                return enumerator.MoveNext();
            }

            public T Current
            {
                get
                {
                    return (T)enumerator.Current;
                }
            }
             
            public virtual void Reset() 
            {
                enumerator.Reset();
            }
                
            object IEnumerator.Current
            {
                get
                {
                    return enumerator.Current;
                }
            }

            public void Dispose() 
            {
            }
            
            public SelectionEnumerator(IEnumerator e) 
            {
                enumerator = e;
            }                                                           
        }

        public virtual IEnumerable<T> Select<T>(IEnumerable e, string predicate) where T:class
        {
            return new SelectionEnumerable<T>(Select(typeof(T), e, predicate));    
        }
 
        public IEnumerable<T> Execute<T>() where T:class
        {      
            if (classExtent == null) 
            {
                throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, cls.FullName);
            }
            switch (classExtentLock) 
            { 
            case ClassExtentLockType.None:
                break;
            case ClassExtentLockType.Shared:
                ((IResource)classExtent).SharedLock();
                break;
            case ClassExtentLockType.Exclusive:
                ((IResource)classExtent).ExclusiveLock();
                break;
            }
            return Execute<T>(classExtent);
        }

        public IEnumerable<T> Execute<T>(IEnumerable iterator) where T:class
        {
            return new SelectionEnumerable<T>(Execute(iterator));
        }
#endif

#if USE_GENERICS
        public virtual IEnumerable<T> Select(IEnumerable<T> iterator, string query)
        {
            cls = typeof(T);
#else
        public virtual IEnumerable Select(Type cls, IEnumerable iterator, string query)
        {
            this.cls = cls;
#endif
            this.query = query;
            buf = query.ToCharArray();
            str = new char[buf.Length];
			
            compile();
            return Execute(iterator);
        }
		
#if !USE_GENERICS
        public virtual IEnumerable Select(string className, IEnumerable enumerable, string query)
        {
            Type cls = Type.GetType(className);
            return Select(cls, enumerable, query);
        }
#endif

#if USE_GENERICS
        public virtual void  Prepare(string query)
        {
            cls = typeof(T);
#else
        public virtual void  Prepare(Type cls, string query)
        {
            this.cls = cls;
#endif
            this.query = query;
            buf = query.ToCharArray();
            str = new char[buf.Length];
            compile();
        }
		
#if !USE_GENERICS
        public virtual void Prepare(string className, string query)
        {
            cls = ClassDescriptor.lookup(storage, className);
            this.query = query;
            buf = query.ToCharArray();
            str = new char[buf.Length];
            compile();
        }
#endif
		
#if USE_GENERICS
        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)Execute().GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Execute().GetEnumerator();
        }
   
        public IEnumerable<T> Execute()
        {      
            if (classExtent == null) 
            {
                throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, cls.FullName);
            }
            switch (classExtentLock) 
            { 
            case ClassExtentLockType.None:
                break;
            case ClassExtentLockType.Shared:
                ((IResource)classExtent).SharedLock();
                break;
            case ClassExtentLockType.Exclusive:
                ((IResource)classExtent).ExclusiveLock();
                break;
            }
            return Execute(classExtent);
        }

        class ExecuteEnumerable<T> : IEnumerable<T>, IEnumerable
        {
           internal ExecuteEnumerable(QueryImpl<T> query, IEnumerable<T> enumerable)
            {
                this.query = query;
                this.enumerable = enumerable;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)query.ExecuteQuery(enumerable).GetEnumerator();
            }

            public IEnumerator<T> GetEnumerator() 
            {
                return query.ExecuteQuery(enumerable).GetEnumerator();
            }

            QueryImpl<T> query;
            IEnumerable<T> enumerable;
        }
            

        public virtual IEnumerable<T> Execute(IEnumerable<T> iterator)
        {
            return new ExecuteEnumerable<T>(this, iterator);
        }

        IEnumerable<T> ExecuteQuery(IEnumerable<T> iterator)
        {
            bool sequentialSearch = false;
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
            DateTime start = new DateTime(0);
            if (storage.listener != null)
            {
                start = DateTime.Now;
            }
#else
            Stopwatch stopwatch = null;
            if (storage.listener != null)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }
#endif
            try
            {
                FieldInfo key = null;
                IEnumerable<T> result = tree != null ? applyIndex(tree, tree, null, out key) : null;
                if (result == null)
                {
                    if (tree == null && order != null && order.next == null)
                    {
                        GenericIndex index = getIndex(cls, order.Name);                    
                        if (index != null)
                        {
                            return filter(index.Range(null, null, order.ascent ? IterationOrder.AscentOrder : IterationOrder.DescentOrder), null);
                        }
                    }
                    if (storage.listener != null)
                    {
                        sequentialSearch = true;
                        storage.listener.SequentialSearchPerformed(query);
                    }   
                    result = filter(iterator, tree);
                }
                else if (order == null || (key != null && order.field == key && order.next == null))
                {
                    return result;
                }
                if (order != null)
                {
                    List<T> list = new List<T>();
                    foreach (T o in result) 
                    {
                        list.Add(o);
                    }
                    if (storage.listener != null) 
                    { 
                        storage.listener.SortResultSetPerformed(query);
                    }
                    sort(list);
                    return list;
                }
                return result;
            } 
            finally 
            {               
                if (storage.listener != null)
                {
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
                    storage.listener.QueryExecution(query, (DateTime.Now-start).Ticks*100, sequentialSearch);
#else
                    stopwatch.Stop();    
                    storage.listener.QueryExecution(query, stopwatch.ElapsedTicks*1000000000L/Stopwatch.Frequency, sequentialSearch);
#endif
                }   
            }
        }
#else
        public IEnumerator GetEnumerator()
        {
            return Execute().GetEnumerator();
        }
   
        public virtual IEnumerable Execute()
        {      
            if (classExtent == null) 
            {
                throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, cls.FullName);
            }
            switch (classExtentLock) 
            { 
            case ClassExtentLockType.None:
                break;
            case ClassExtentLockType.Shared:
                ((IResource)classExtent).SharedLock();
                break;
            case ClassExtentLockType.Exclusive:
                ((IResource)classExtent).ExclusiveLock();
                break;
            }
            return Execute(classExtent);
        }


        class ExecuteEnumerable : IEnumerable
        {
           internal ExecuteEnumerable(QueryImpl query, IEnumerable enumerable)
            {
                this.query = query;
                this.enumerable = enumerable;
            }

            public IEnumerator GetEnumerator() 
            {
                return query.ExecuteQuery(enumerable).GetEnumerator();
            }

            QueryImpl query;
            IEnumerable enumerable;
        }
            
        public virtual IEnumerable Execute(IEnumerable iterator)
        {
            return new ExecuteEnumerable(this, iterator);
        }

        IEnumerable ExecuteQuery(IEnumerable iterator)
        {
            bool sequentialSearch = false;
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
            DateTime start = new DateTime(0);
            if (storage.listener != null)
            {
                start = DateTime.Now;
            }
#else
            Stopwatch stopwatch = null;
            if (storage.listener != null)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }
#endif
            try
            {
                FieldInfo key = null;
                IEnumerable result = tree != null ? applyIndex(tree, tree, null, out key) : null;
                if (result == null)
                {
                    if (tree == null && order != null && order.next == null)
                    {
                        GenericIndex index = getIndex(cls, order.Name);                    
                        if (index != null)
                        {
                            return filter(index.Range(null, null, order.ascent ? IterationOrder.AscentOrder : IterationOrder.DescentOrder), null);
                        }
                    }
                    if (storage.listener != null)
                    {
                        sequentialSearch = true;
                        storage.listener.SequentialSearchPerformed(query);
                    }   
                    result = new FilterIterator(this, iterator, tree);
                }
                else if (order == null || (key != null && order.field == key && order.next == null))
                {
                    return result;
                }
                if (order != null)
                {
                    ArrayList list = new ArrayList();
                    foreach (object o in result) 
                    {
                        list.Add(o);
                    }
                    if (storage.listener != null) 
                    { 
                        storage.listener.SortResultSetPerformed(query);
                    }
                    sort(list);
                    return list;
                }
                return result;
            }
            finally 
            {               
                if (storage.listener != null)
                {
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
                    storage.listener.QueryExecution(query, (DateTime.Now-start).Ticks*100, sequentialSearch);
#else
                    stopwatch.Stop();    
                    storage.listener.QueryExecution(query, stopwatch.ElapsedTicks*1000000000L/Stopwatch.Frequency, sequentialSearch);
#endif
                }   
            }
        }
#endif
 		
#if USE_GENERICS
        private void  sort(List<T> selection)
        {
            T top;
#else
        private void  sort(ArrayList selection)
        {
            object top;
#endif
            int i, j, k, n;
            OrderNode order = this.order;
			
            if (selection.Count == 0)
            {
                return ;
            }
            for (OrderNode ord = order; ord != null; ord = ord.next)
            {
                if (ord.fieldName != null)
                {
                    ord.resolveName(selection[0].GetType());
                }
            }
			
            for (n = selection.Count, i = n / 2, j = i; i >= 1; i--)
            {
                k = i;
                top = selection[k - 1];
                do 
                {
                    if (k * 2 == n || order.compare(selection[k * 2 - 1], selection[k * 2]) > 0)
                    {
                        if (order.compare(top, selection[k * 2 - 1]) >= 0)
                        {
                            break;
                        }
                        selection[k - 1] = selection[k * 2 - 1];
                        k = k * 2;
                    }
                    else
                    {
                        if (order.compare(top, selection[k * 2]) >= 0)
                        {
                            break;
                        }
                        selection[k - 1] = selection[k * 2];
                        k = k * 2 + 1;
                    }
                }
                while (k <= j);
                selection[k - 1] = top;
            }
            for (i = n; i >= 2; i--)
            {
                top = selection[i - 1];
                selection[i - 1] = selection[0];
                selection[0] = top;
                for (k = 1, j = (i - 1) / 2; k <= j; )
                {
                    if (k * 2 == i - 1 || order.compare(selection[k * 2 - 1], selection[k * 2]) > 0)
                    {
                        if (order.compare(top, selection[k * 2 - 1]) >= 0)
                        {
                            break;
                        }
                        selection[k - 1] = selection[k * 2 - 1];
                        k = k * 2;
                    }
                    else
                    {
                        if (order.compare(top, selection[k * 2]) >= 0)
                        {
                            break;
                        }
                        selection[k - 1] = selection[k * 2];
                        k = k * 2 + 1;
                    }
                }
                selection[k - 1] = top;
            }
        }
		
        public void SetClass(Type cls) 
        { 
            this.cls = cls;
        }
    
        public CodeGenerator GetCodeGenerator()
        {
            return GetCodeGenerator(cls);
        }
    
        public CodeGenerator GetCodeGenerator(Type cls)
        {
            order = null;
            tree = null;
            parameters.Clear();
            return new CodeGeneratorImpl(this, cls);
        }

#if USE_GENERICS
        public virtual void  AddIndex(string key, GenericKeyIndex<T> index)
        {
            if (indices == null)
            {
                indices = new Dictionary<string,GenericIndex>();
            }
#else
        public virtual void  AddIndex(string key, GenericIndex index)
        {
            if (indices == null)
            {
                indices = new Hashtable();
            }
#endif
            indices[key] = index;
        }
		
#if USE_GENERICS
        private GenericIndex getIndex(Type type, string key)
        {
            GenericIndex index;
            if (indices != null && cls == type && indices.TryGetValue(key, out index))         
            { 
                return index;
            }
            if (indexProvider != null) 
            { 
                return indexProvider.GetIndex(type, key);
            }
            return null;
        }
#else
        private GenericIndex getIndex(Type type, string key)
        {
            if (indices != null && cls == type) 
            { 
                GenericIndex index = (GenericIndex)indices[key];
                if (index != null) 
                { 
                    return index;
                }
            }
            if (indexProvider != null) 
            { 
                return indexProvider.GetIndex(type, key);
            }
            return null;
        }
#endif
		
        internal Token scan()
        {
            int p = pos;
            int eol = buf.Length;
            char ch = (char) (0);
            int i;
            while (p < eol && System.Char.IsWhiteSpace(ch = buf[p]))
            {
                p += 1;
            }
            if (p == eol)
            {
                return Token.tknEof;
            }
            pos = ++p;
            switch (ch)
            {
				
                case '+': 
                    return Token.tknAdd;
				
                case '-': 
                    return Token.tknSub;
				
                case '*': 
                    return Token.tknMul;
				
                case '/': 
                    return Token.tknDiv;
				
                case '.': 
                    return Token.tknDot;
				
                case ',': 
                    return Token.tknComma;
				
                case '(': 
                    return Token.tknLpar;
				
                case ')': 
                    return Token.tknRpar;
				
                case '[': 
                    return Token.tknLbr;
				
                case ']': 
                    return Token.tknRbr;
				
                case ':': 
                    return Token.tknCol;
				
                case '^': 
                    return Token.tknPower;
				
                case '?': 
                    return Token.tknParam;
				
                case '<': 
                    if (p < eol)
                    {
                        if (buf[p] == '=')
                        {
                            pos += 1;
                            return Token.tknLe;
                        }
                        if (buf[p] == '>')
                        {
                            pos += 1;
                            return Token.tknNe;
                        }
                    }
                    return Token.tknLt;
				
                case '>': 
                    if (p < eol && buf[p] == '=')
                    {
                        pos += 1;
                        return Token.tknGe;
                    }
                    return Token.tknGt;
				
                case '=': 
                    return Token.tknEq;
				
                case '!': 
                    if (p == eol || buf[p] != '=')
                    {
                        throw new CompileError("Invalid token '!'", p - 1);
                    }
                    pos += 1;
                    return Token.tknNe;
				
                case '|': 
                    if (p == eol || buf[p] != '|')
                    {
                        throw new CompileError("Invalid token '!'", p - 1);
                    }
                    pos += 1;
                    return Token.tknAdd;
				
                case '\'': 
                    i = 0;
                    while (true)
                    {
                        if (p == eol)
                        {
                            throw new CompileError("Unexpected end of string constant", p);
                        }
                        if (buf[p] == '\'')
                        {
                            if (++p == eol || buf[p] != '\'')
                            {
                                svalue = new string(str, 0, i);
                                pos = p;
                                return Token.tknSconst;
                            }
                        }
                        str[i++] = buf[p++];
                    }
				
                case '0': 
                case '1': 
                case '2': 
                case '3': 
                case '4': 
                case '5': 
                case '6': 
                case '7': 
                case '8': 
                case '9': 
                    i = p - 1;
                    while (p < eol && System.Char.IsDigit(ch = buf[p]))
                    {
                        p += 1;
                    }
                    if (ch == '.' || ch == 'e' || ch == 'E')
                    {
                        while (++p < eol && (System.Char.IsDigit(buf[p]) || buf[p] == 'e' || buf[p] == 'E' || buf[p] == '.' || ((ch == 'e' || ch == 'E') && (buf[p] == '-' || buf[p] == '+'))))
                            ;
                        pos = p;
                        try
                        {
                            fvalue = double.Parse(query.Substring(i, p - i));
                        }
                        catch (System.FormatException)
                        {
                            throw new CompileError("Bad floating point constant", i);
                        }
                        return Token.tknFconst;
                    }
                    else
                    {
                        pos = p;
                        try
                        {
                            ivalue = System.Convert.ToInt64(query.Substring(i, p - i), 10);
                        }
                        catch (System.FormatException)
                        {
                            throw new CompileError("Bad floating point constant", i);
                        }
                        return Token.tknIconst;
                    }
				
                default: 
                    if (System.Char.IsLetter(ch) || ch == '$' || ch == '_')
                    {
                        i = p - 1;
                        while (p < eol && (System.Char.IsLetterOrDigit(ch = buf[p]) || ch == '$' || ch == '_'))
                        {
                            p += 1;
                        }
                        pos = p;
                        ident = query.Substring(i, p - i);
                        Symbol s = (Symbol) symtab[ident.ToLower()];
                        return (s == null)?Token.tknIdent:s.tkn;
                    }
                    else
                    {
                        throw new CompileError("Invalid symbol: " + ch, p - 1);
                    }
            }
        }
		
		
        internal Node disjunction()
        {
            Node left = conjunction();
            if (lex == Token.tknOr)
            {
                int p = pos;
                Node right = disjunction();
                if (left.type == NodeType.tpInt && right.type == NodeType.tpInt)
                {
                    left = new BinOpNode(NodeType.tpInt, NodeTag.opIntOr, left, right);
                }
                else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool)
                {
                    left = new BinOpNode(NodeType.tpBool, NodeTag.opBoolOr, left, right);
                }
                else if (left.type == NodeType.tpAny || right.type == NodeType.tpAny)
                {
                    left = new BinOpNode(NodeType.tpAny, NodeTag.opAnyOr, left, right);
                }
                else
                {
                    throw new CompileError("Bad operands for OR operator", p);
                }
            }
            return left;
        }
		
        internal Node conjunction()
        {
            Node left = comparison();
            if (lex == Token.tknAnd)
            {
                int p = pos;
                Node right = conjunction();
                if (left.type == NodeType.tpInt && right.type == NodeType.tpInt)
                {
                    left = new BinOpNode(NodeType.tpInt, NodeTag.opIntAnd, left, right);
                }
                else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool)
                {
                    left = new BinOpNode(NodeType.tpBool, NodeTag.opBoolAnd, left, right);
                }
                else if (left.type == NodeType.tpAny || right.type == NodeType.tpAny)
                {
                    left = new BinOpNode(NodeType.tpAny, NodeTag.opAnyAnd, left, right);
                }
                else
                {
                    throw new CompileError("Bad operands for AND operator", p);
                }
            }
            return left;
        }
		
		
        internal Node compare(Node expr, BinOpNode list)
        {
            BinOpNode tree = null; 
            int n = 1;
            do { 
                Node elem = list.right;
                NodeTag cop = NodeTag.opNop;
                if (elem.type == NodeType.tpUnknown)
                {
                    elem.type = expr.type;
                }
                if (expr.type == NodeType.tpInt)
                {
                    if (elem.type == NodeType.tpReal)
                    {
                        expr = new UnaryOpNode(NodeType.tpReal, NodeTag.opIntToReal, expr);
                        cop = NodeTag.opRealEq;
                    }
                    else if (elem.type == NodeType.tpInt)
                    {
                        cop = NodeTag.opIntEq;
                    }
                }
                else if (expr.type == NodeType.tpReal)
                {
                    if (elem.type == NodeType.tpReal)
                    {
                        cop = NodeTag.opRealEq;
                    }
                    else if (elem.type == NodeType.tpInt)
                    {
                        cop = NodeTag.opRealEq;
                        elem = int2real(elem);
                    }
                }
                else if (expr.type == NodeType.tpStr && elem.type == NodeType.tpStr)
                {
                    cop = isCaseInsensitive(expr) ? NodeTag.opStrIgnoreCaseEq : NodeTag.opStrEq;
                }
                else if (expr.type == NodeType.tpDate && elem.type == NodeType.tpDate)
                {
                    cop = NodeTag.opDateEq;
                }
                else if (expr.type == NodeType.tpObj && elem.type == NodeType.tpObj)
                {
                    cop = NodeTag.opObjEq;
                }
                else if (expr.type == NodeType.tpBool && elem.type == NodeType.tpBool)
                {
                    cop = NodeTag.opBoolEq;
                }
                else if (expr.type == NodeType.tpAny)
                {
                    cop = NodeTag.opAnyEq;
                }
                if (cop == NodeTag.opNop)
                {
                    throw new CompileError("Expression " + n + " in right part of IN " + "operator has incompatible type", pos);
                }
                n += 1;
                BinOpNode cmp = new BinOpNode(NodeType.tpBool, cop, expr, elem);
                if (tree == null) 
                { 
                    tree = cmp; 
                } 
                else 
                {
                    tree = new BinOpNode(NodeType.tpBool, NodeTag.opBoolOr, cmp, tree);
                }
            } while ((list = (BinOpNode)list.left) != null);

            return tree;
        }

        internal Node comparison()
        {
            int leftPos = pos;
            Node left, right;
            left = addition();
            Token cop = lex;
            if (cop == Token.tknEq || cop == Token.tknNe || cop == Token.tknGt || cop == Token.tknGe || cop == Token.tknLe || cop == Token.tknLt || cop == Token.tknBetween || cop == Token.tknLike || cop == Token.tknNot || cop == Token.tknIs || cop == Token.tknIn)
            {
                int rightPos = pos;
                bool not = false;
                if (cop == Token.tknNot)
                {
                    not = true;
                    cop = scan();
                    if (cop != Token.tknLike && cop != Token.tknBetween && cop != Token.tknIn)
                    {
                        throw new CompileError("LIKE, BETWEEN or IN expected", rightPos);
                    }
                    rightPos = pos;
                }
                else if (cop == Token.tknIs)
                {
                    if (left.type < NodeType.tpObj)
                    {
                        throw new CompileError("IS [NOT] NULL predicate can be applied only to references,arrays or string", rightPos);
                    }
                    rightPos = pos;
                    if ((cop = scan()) == Token.tknNull)
                    {
                        left = new UnaryOpNode(NodeType.tpBool, NodeTag.opIsNull, left);
                    }
                    else if (cop == Token.tknNot)
                    {
                        rightPos = pos;
                        if (scan() == Token.tknNull)
                        {
                            left = new UnaryOpNode(NodeType.tpBool, NodeTag.opBoolNot, new UnaryOpNode(NodeType.tpBool, NodeTag.opIsNull, left));
                        }
                        else
                        {
                            throw new CompileError("NULL expected", rightPos);
                        }
                    }
                    else
                    {
                        throw new CompileError("[NOT] NULL expected", rightPos);
                    }
                    lex = scan();
                    return left;
                }
                right = addition();
                if (cop == Token.tknIn)
                {
                    if (right == singleElementList) 
                    { 
                        right = new BinOpNode(NodeType.tpList, NodeTag.opNop, null, right);
                    }          
                    if (right.type != NodeType.tpList && (left.type == NodeType.tpAny || right.type == NodeType.tpAny || right.type == NodeType.tpUnknown))
                    {
                        left = new BinOpNode(NodeType.tpBool, NodeTag.opInAny, left, right);
                    }
                    else
                    {
                        switch (right.type)
                        {
							
                            case NodeType.tpCollection: 
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanCollection, left, right);
                                break;
							
                            case NodeType.tpArrayBool: 
                                if (left.type != NodeType.tpBool)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayBool, left, right);
                                break;
							
                            case NodeType.tpArrayInt1: 
                                if (left.type != NodeType.tpInt)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt1, left, right);
                                break;
							
                            case NodeType.tpArrayChar: 
                                if (left.type != NodeType.tpInt)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayChar, left, right);
                                break;
							
                            case NodeType.tpArrayInt2: 
                                if (left.type != NodeType.tpInt)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt2, left, right);
                                break;
							
                            case NodeType.tpArrayInt4: 
                                if (left.type != NodeType.tpInt)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt4, left, right);
                                break;
							
                            case NodeType.tpArrayInt8: 
                                if (left.type != NodeType.tpInt)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt8, left, right);
                                break;
							
                            case NodeType.tpArrayReal4: 
                                if (left.type == NodeType.tpInt)
                                {
                                    left = int2real(left);
                                }
                                else if (left.type != NodeType.tpReal)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayReal4, left, right);
                                break;
							
                            case NodeType.tpArrayReal8: 
                                if (left.type == NodeType.tpInt)
                                {
                                    left = int2real(left);
                                }
                                else if (left.type != NodeType.tpReal)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayReal8, left, right);
                                break;
							
                            case NodeType.tpArrayObj: 
                                if (left.type != NodeType.tpObj)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayObj, left, right);
                                break;
							
                            case NodeType.tpArrayStr: 
                                if (left.type != NodeType.tpStr)
                                {
                                    throw new CompileError("Incompatible types of IN operator operands", rightPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayStr, left, right);
                                break;
							
                            case NodeType.tpStr: 
                                if (left.type != NodeType.tpStr)
                                {
                                    throw new CompileError("Left operand of IN expression hasn't string type", leftPos);
                                }
                                left = new BinOpNode(NodeType.tpBool, NodeTag.opInString, left, right);
                                break;
							
                            case NodeType.tpList: 
                                left = compare(left, (BinOpNode) right);
                                break;
							
                            default: 
                                throw new CompileError("List of expressions or array expected", rightPos);
							
                        }
                    }
                }
                else if (cop == Token.tknBetween)
                {
                    int andPos = pos;
                    if (lex != Token.tknAnd)
                    {
                        throw new CompileError("AND expected", pos);
                    }
                    Node right2 = addition();
                    if (right.type == NodeType.tpUnknown)
                    {
                        right.type = left.type;
                    }
                    if (right2.type == NodeType.tpUnknown)
                    {
                        right2.type = left.type;
                    }
                    if (left.type == NodeType.tpAny || right.type == NodeType.tpAny || right2.type == NodeType.tpAny)
                    {
                        left = new CompareNode(NodeTag.opAnyBetween, left, right, right2);
                    }
                    else if (left.type == NodeType.tpReal || right.type == NodeType.tpReal || right2.type == NodeType.tpReal)
                    {
                        if (left.type == NodeType.tpInt)
                        {
                            left = int2real(left);
                        }
                        else if (left.type != NodeType.tpReal)
                        {
                            throw new CompileError("operand of BETWEEN operator should be of integer, real or string type", leftPos);
                        }
                        if (right.type == NodeType.tpInt)
                        {
                            right = int2real(right);
                        }
                        else if (right.type != NodeType.tpReal)
                        {
                            throw new CompileError("operand of BETWEEN operator should be of integer, real or string type", rightPos);
                        }
                        if (right2.type == NodeType.tpInt)
                        {
                            right2 = int2real(right2);
                        }
                        else if (right2.type != NodeType.tpReal)
                        {
                            throw new CompileError("operand of BETWEEN operator should be of integer, real or string type", andPos);
                        }
                        left = new CompareNode(NodeTag.opRealBetween, left, right, right2);
                    }
                    else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt && right2.type == NodeType.tpInt)
                    {
                        left = new CompareNode(NodeTag.opIntBetween, left, right, right2);
                    }
                    else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr && right2.type == NodeType.tpStr)
                    {
                        left = new CompareNode(isCaseInsensitive(left) ? NodeTag.opStrIgnoreCaseBetween : NodeTag.opStrBetween, left, right, right2);
                    }
                    else if (left.type == NodeType.tpDate) 
                    { 
                        if (right.type == NodeType.tpStr)
                        {
                            right = str2date(right);
                        }
                        else if (right.type != NodeType.tpDate)
                        {
                            throw new CompileError("operands of BETWEEN operator should be of date type", rightPos);
                        }
                        if (right2.type == NodeType.tpStr)
                        {
                            right2 = str2date(right2);
                        }
                        else if (right2.type != NodeType.tpDate)
                        {
                            throw new CompileError("operands of BETWEEN operator should be of date type", andPos);
                        }
                        left = new CompareNode(NodeTag.opDateBetween, left, right, right2);
                    }
                    else
                    {
                        throw new CompileError("operands of BETWEEN operator should be of integer, real or string type", rightPos);
                    }
                }
                else if (cop == Token.tknLike)
                {
                    if (right.type == NodeType.tpUnknown)
                    {
                        right.type = left.type;
                    }
                    if (left.type == NodeType.tpAny)
                    {
                        left = new ConvertAnyNode(NodeType.tpStr, left);
                    }
                    if (right.type == NodeType.tpAny)
                    {
                        right = new ConvertAnyNode(NodeType.tpStr, right);
                    }
                    if (left.type != NodeType.tpStr || right.type != NodeType.tpStr)
                    {
                        throw new CompileError("operands of LIKE operator should be of string type", rightPos);
                    }
                    if (lex == Token.tknEscape)
                    {
                        rightPos = pos;
                        if (scan() != Token.tknSconst)
                        {
                            throw new CompileError("String literal espected after ESCAPE", rightPos);
                        }
                        left = new CompareNode(isCaseInsensitive(left) ? NodeTag.opStrIgnoreCaseLikeEsc : NodeTag.opStrLikeEsc, left, right, new StrLiteralNode(svalue));
                        lex = scan();
                    }
                    else
                    {
                        left = new CompareNode(isCaseInsensitive(left) ? NodeTag.opStrIgnoreCaseLike : NodeTag.opStrLike, left, right, null);
                    }
                }
                else
                {
                    if (right.type == NodeType.tpUnknown)
                    {
                        right.type = left.type;
                    }
                    if (left.type == NodeType.tpUnknown)
                    {
                        left.type = right.type;
                    }
                    if (left.type == NodeType.tpAny || right.type == NodeType.tpAny)
                    {
                        left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opAnyEq + (int)cop - (int)Token.tknEq), left, right);
                    }
                    else if (left.type == NodeType.tpReal || right.type == NodeType.tpReal)
                    {
                        if (left.type == NodeType.tpInt)
                        {
                            left = int2real(left);
                        }
                        else if (left.type != NodeType.tpReal)
                        {
                            throw new CompileError("operands of relation operator should be of intger, real or string type", leftPos);
                        }
                        if (right.type == NodeType.tpInt)
                        {
                            right = int2real(right);
                        }
                        else if (right.type != NodeType.tpReal)
                        {
                            throw new CompileError("operands of relation operator should be of intger, real or string type", rightPos);
                        }
                        left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opRealEq + (int)cop - (int)Token.tknEq), left, right);
                    }
                    else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt)
                    {
                        left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opIntEq + (int)cop - (int)Token.tknEq), left, right);
                    }
                    else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr)
                    {
                        NodeTag strCmpOp = (NodeTag)((int)((isCaseInsensitive(left) || isCaseInsensitive(right))
                            ? NodeTag.opStrIgnoreCaseEq : NodeTag.opStrEq) + (int)cop - (int)Token.tknEq);
                        left = new BinOpNode(NodeType.tpBool, strCmpOp, left, right);
                    }
                    else if (left.type == NodeType.tpDate)
                    {
                        if (right.type == NodeType.tpStr)
                        {
                            right = str2date(right);
                        }
                        else if (right.type != NodeType.tpDate)
                        {
                            throw new CompileError("right operand of relation operator should be of date type", rightPos);
                        }
                        left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opDateEq + (int)cop - (int)Token.tknEq), left, right);
                    }
                    else if (left.type == NodeType.tpObj && right.type == NodeType.tpObj)
                    {
                        if (cop == Token.tknEq && cop == Token.tknNe)
                        {
                            left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opObjEq + (int)cop - (int)Token.tknEq), left, right);
                        }
                        else
                        {
                            left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opAnyEq + (int)cop - (int)Token.tknEq), left, right);
                        }
                    }
                    else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool)
                    {
                        if (cop != Token.tknEq && cop != Token.tknNe)
                        {
                            throw new CompileError("Boolean variables can be checked only for equality", rightPos);
                        }
                        left = new BinOpNode(NodeType.tpBool, (NodeTag)((int)NodeTag.opBoolEq + (int)cop - (int)Token.tknEq), left, right);
                    }
                    else
                    {
                        throw new CompileError("operands of relation operator should be of integer, real or string type", rightPos);
                    }
                }
                if (not)
                {
                    left = new UnaryOpNode(NodeType.tpBool, NodeTag.opBoolNot, left);
                }
            }
            return left;
        }
		
		
        internal Node addition()
        {
            int leftPos = pos;
            Node left = multiplication();
            while (lex == Token.tknAdd || lex == Token.tknSub)
            {
                Token cop = lex;
                int rightPos = pos;
                Node right = multiplication();
                if (left.type == NodeType.tpAny || right.type == NodeType.tpAny)
                {
                    left = new BinOpNode(NodeType.tpAny, cop == Token.tknAdd?NodeTag.opAnyAdd:NodeTag.opAnySub, left, right);
                }
                else if (left.type == NodeType.tpReal || right.type == NodeType.tpReal)
                {
                    if (left.type == NodeType.tpInt)
                    {
                        left = int2real(left);
                    }
                    else if (left.type != NodeType.tpReal)
                    {
                        throw new CompileError("operands of arithmetic operators should be of integer or real type", leftPos);
                    }
                    if (right.type == NodeType.tpInt)
                    {
                        right = int2real(right);
                    }
                    else if (right.type != NodeType.tpReal)
                    {
                        throw new CompileError("operands of arithmetic operator should be of integer or real type", rightPos);
                    }
                    left = new BinOpNode(NodeType.tpReal, cop == Token.tknAdd?NodeTag.opRealAdd:NodeTag.opRealSub, left, right);
                }
                else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt)
                {
                    left = new BinOpNode(NodeType.tpInt, cop == Token.tknAdd?NodeTag.opIntAdd:NodeTag.opIntSub, left, right);
                }

                else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr)
                {
                    if (cop == Token.tknAdd)
                    {
                        left = new BinOpNode(NodeType.tpStr, NodeTag.opStrConcat, left, right);
                    }
                    else
                    {
                        throw new CompileError("Operation - is not defined for strings", rightPos);
                    }
                }
                else
                {
                    throw new CompileError("operands of arithmentic operator should be of integer or real type", rightPos);
                }
                leftPos = rightPos;
            }
            return left;
        }
		
		
        internal Node multiplication()
        {
            int leftPos = pos;
            Node left = power();
            while (lex == Token.tknMul || lex == Token.tknDiv)
            {
                Token cop = lex;
                int rightPos = pos;
                Node right = power();
                if (left.type == NodeType.tpAny || right.type == NodeType.tpAny)
                {
                    left = new BinOpNode(NodeType.tpAny, cop == Token.tknMul?NodeTag.opAnyMul:NodeTag.opAnyDiv, left, right);
                }
                else if (left.type == NodeType.tpReal || right.type == NodeType.tpReal)
                {
                    if (left.type == NodeType.tpInt)
                    {
                        left = int2real(left);
                    }
                    else if (left.type != NodeType.tpReal)
                    {
                        throw new CompileError("operands of arithmetic operators should be of integer or real type", leftPos);
                    }
                    if (right.type == NodeType.tpInt)
                    {
                        right = int2real(right);
                    }
                    else if (right.type != NodeType.tpReal)
                    {
                        throw new CompileError("operands of arithmetic operator should be of integer or real type", rightPos);
                    }
                    left = new BinOpNode(NodeType.tpReal, cop == Token.tknMul?NodeTag.opRealMul:NodeTag.opRealDiv, left, right);
                }
                else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt)
                {
                    left = new BinOpNode(NodeType.tpInt, cop == Token.tknMul?NodeTag.opIntMul:NodeTag.opIntDiv, left, right);
                }
                else
                {
                    throw new CompileError("operands of arithmentic operator should be of integer or real type", rightPos);
                }
                leftPos = rightPos;
            }
            return left;
        }
		
		
        internal Node power()
        {
            int leftPos = pos;
            Node left = term();
            if (lex == Token.tknPower)
            {
                int rightPos = pos;
                Node right = power();
                if (left.type == NodeType.tpAny || right.type == NodeType.tpAny)
                {
                    left = new BinOpNode(NodeType.tpAny, NodeTag.opAnyPow, left, right);
                }
                else if (left.type == NodeType.tpReal || right.type == NodeType.tpReal)
                {
                    if (left.type == NodeType.tpInt)
                    {
                        left = int2real(left);
                    }
                    else if (left.type != NodeType.tpReal)
                    {
                        throw new CompileError("operands of arithmetic operators should be of integer or real type", leftPos);
                    }
                    if (right.type == NodeType.tpInt)
                    {
                        right = int2real(right);
                    }
                    else if (right.type != NodeType.tpReal)
                    {
                        throw new CompileError("operands of arithmetic operator should be of integer or real type", rightPos);
                    }
                    left = new BinOpNode(NodeType.tpReal, NodeTag.opRealPow, left, right);
                }
                else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt)
                {
                    left = new BinOpNode(NodeType.tpInt, NodeTag.opIntPow, left, right);
                }
                else
                {
                    throw new CompileError("operands of arithmentic operator should be of integer or real type", rightPos);
                }
            }
            return left;
        }
		
		
        internal Node component(Node baseExpr, Type cls)
        {
            string ident = this.ident;
            FieldInfo f;
            lex = scan();
            if (lex != Token.tknLpar)
            {
                if (baseExpr == null && contains != null)
                {
                    f = lookupField(contains.containsFieldClass, ident);
                    if (f != null)
                    {
                        return new ElementNode(contains.containsExpr.FieldName, f);
                    }
                }
                else if (cls != null)
                {
                    f = lookupField(cls, ident);
                    if (f != null)
                    {
                        return new LoadNode(baseExpr, f);
                    }
                }
            }
            Type[] profile = defaultProfile;
            Node[] arguments = noArguments;
            if (lex == Token.tknLpar)
            {
                ArrayList argumentList = new ArrayList();
                do 
                {
                    argumentList.Add(disjunction());
                }
                while (lex == Token.tknComma);
                if (lex != Token.tknRpar)
                {
                    throw new CompileError("')' expected", pos);
                }
                lex = scan();
                profile = new Type[argumentList.Count];
                arguments = new Node[profile.Length];
                bool unknownProfile = false;
                for (int i = 0; i < profile.Length; i++)
                {
                    Node arg = (Node) argumentList[i];
                    arguments[i] = arg;
                    Type argType;
                    switch (arg.type)
                    {				
                        case NodeType.tpInt: 
                            argType = typeof(long);
                            break;
						
                        case NodeType.tpReal: 
                            argType = typeof(double);
                            break;
						
                        case NodeType.tpStr: 
                            argType = typeof(string);
                            break;

                        case NodeType.tpDate: 
                            argType = typeof(DateTime);
                            break;
						
                        case NodeType.tpBool: 
                            argType = typeof(bool);
                            break;
						
                        case NodeType.tpObj: 
                            argType = arg.Type;
                            break;
						
                        case NodeType.tpArrayBool: 
                            argType = typeof(bool[]);
                            break;
						
                        case NodeType.tpArrayChar: 
                            argType = typeof(char[]);
                            break;
						
                        case NodeType.tpArrayInt1: 
                            argType = typeof(sbyte[]);
                            break;
						
                        case NodeType.tpArrayInt2: 
                            argType = typeof(short[]);
                            break;
						
                        case NodeType.tpArrayInt4: 
                            argType = typeof(int[]);
                            break;
						
                        case NodeType.tpArrayInt8: 
                            argType = typeof(long[]);
                            break;
						
                        case NodeType.tpArrayUInt1: 
                            argType = typeof(byte);
                            break;
						
                        case NodeType.tpArrayUInt2: 
                            argType = typeof(ushort);
                            break;
						
                        case NodeType.tpArrayUInt4: 
                            argType = typeof(uint);
                            break;
						
                        case NodeType.tpArrayUInt8: 
                            argType = typeof(long);
                            break;
						
                        case NodeType.tpArrayReal4: 
                            argType = typeof(float[]);
                            break;
						
                        case NodeType.tpArrayReal8: 
                            argType = typeof(double[]);
                            break;
						
                        case NodeType.tpArrayStr: 
                            argType = typeof(string[]);
                            break;
						
                        case NodeType.tpArrayObj: 
                            argType = typeof(object[]);
                            break;
						
                        case NodeType.tpUnknown: 
                        case NodeType.tpAny: 
                            argType = typeof(object);
                            unknownProfile = true;
                            break;
						
                        default: 
                            throw new CompileError("Invalid method argument type", pos);
						
                    }
                    profile[i] = argType;
                }
                if (unknownProfile)
                {
                    if (!cls.Equals(typeof(object)) || baseExpr != null || contains == null)
                    {
                        return new InvokeAnyNode(baseExpr, ident, arguments, null);
                    }
                    else
                    {
                        return new InvokeAnyNode(baseExpr, ident, arguments, contains.containsExpr.FieldName);
                    }
                }
            }
            MethodInfo m = null;
            if (baseExpr == null && contains != null)
            {
                m = lookupMethod(contains.containsFieldClass, ident, profile);
                if (m != null)
                {
                    return new InvokeElementNode(m, arguments, contains.containsExpr.FieldName);
                }
                if (arguments == noArguments && cls != null)
                {
                    f = lookupField(cls, ident);
                    if (f != null)
                    {
                        return new LoadNode(baseExpr, f);
                    }
                }
            }
            if (cls != null)
            {
                m = lookupMethod(cls, ident, profile);
                if (m != null)
                {
                    return new InvokeNode(baseExpr, m, arguments);
                }
            }
            if (typeof(object).Equals(cls))
            {
                return profile.Length == 0
                    ? (Node)new LoadAnyNode(baseExpr, ident, null)
                    : (Node)new InvokeAnyNode(baseExpr, ident, arguments, null);
            }
            else if (baseExpr == null && contains != null && contains.containsFieldClass.Equals(typeof(object)))
            {
                string arrFieldName = contains.containsExpr.FieldName;
                return profile.Length == 0
                    ? (Node)new LoadAnyNode(baseExpr, ident, arrFieldName)
                    : (Node)new InvokeAnyNode(baseExpr, ident, arguments, arrFieldName);
            }
            else
            {
                throw new CompileError("No field or method '" + ident + "' in class " + (cls == null?contains.containsFieldClass:cls).FullName, pos);
            }
        }
		
		
        internal Node field(Node expr)
        {
            int p = pos;
            NodeType type;
            NodeTag  tag;
            Type cls = expr.Type;
            while (true)
            {
                if (resolveMap != null && expr.type == NodeType.tpObj && cls != null)
                {
                    ResolveMapping rm = (ResolveMapping) resolveMap[cls];
                    if (rm != null)
                    {
                        expr = new ResolveNode(expr, rm.resolver, rm.resolved);
                        cls = rm.resolved;
                    }
                }
                switch (lex)
                {
					
                    case Token.tknDot: 
                        if (scan() != Token.tknIdent)
                        {
                            throw new CompileError("identifier expected", p);
                        }
                        if (expr.type != NodeType.tpObj && expr.type != NodeType.tpAny && expr.type != NodeType.tpCollection)
                        {
                            throw new CompileError("Left operand of '.' should be reference", p);
                        }
                        if (contains != null && contains.containsExpr.Equals(expr))
                        {
                            expr = component(null, null);
                        }
                        else
                        {
                            if (expr.type == NodeType.tpCollection)
                            {
                                throw new CompileError("Left operand of '.' should be reference", p);
                            }
                            expr = component(expr, cls);
                        }
                        cls = expr.Type;
                        continue;
					
                    case Token.tknLbr: 
                    switch (expr.type)
                    {
							
                        case NodeType.tpArrayBool: 
                            tag = NodeTag.opGetAtBool;
                            type = NodeType.tpBool;
                            break;
							
                        case NodeType.tpArrayChar: 
                            tag = NodeTag.opGetAtChar;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpStr: 
                            tag = NodeTag.opStrGetAt;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayInt1: 
                            tag = NodeTag.opGetAtInt1;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayInt2: 
                            tag = NodeTag.opGetAtInt2;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayInt4: 
                            tag = NodeTag.opGetAtInt4;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayInt8: 
                            tag = NodeTag.opGetAtInt8;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayUInt1: 
                            tag = NodeTag.opGetAtUInt1;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayUInt2: 
                            tag = NodeTag.opGetAtUInt2;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayUInt4: 
                            tag = NodeTag.opGetAtUInt4;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayUInt8: 
                            tag = NodeTag.opGetAtUInt8;
                            type = NodeType.tpInt;
                            break;
							
                        case NodeType.tpArrayReal4: 
                            tag = NodeTag.opGetAtReal4;
                            type = NodeType.tpReal;
                            break;
							
                        case NodeType.tpArrayReal8: 
                            tag = NodeTag.opGetAtReal8;
                            type = NodeType.tpReal;
                            break;
							
                        case NodeType.tpArrayStr: 
                            tag = NodeTag.opGetAtStr;
                            type = NodeType.tpStr;
                            break;
							
                        case NodeType.tpArrayObj: 
                            tag = NodeTag.opGetAtObj;
                            cls = cls.GetElementType();
                            type = cls.IsArray?NodeType.tpArrayObj:cls.Equals(typeof(object))?NodeType.tpAny:NodeType.tpObj;
                            break;
							
                        case NodeType.tpAny: 
                            tag = NodeTag.opGetAtObj;
                            type = NodeType.tpAny;
                            break;
							
                        default: 
                            throw new CompileError("Index can be applied only to arrays", p);
							
                    }
                        p = pos;
                        Node index = disjunction();
                        if (lex != Token.tknRbr)
                        {
                            throw new CompileError("']' expected", pos);
                        }
                        if (index.type == NodeType.tpAny)
                        {
                            index = new ConvertAnyNode(NodeType.tpInt, index);
                        }
                        else if (index.type != NodeType.tpInt && index.type != NodeType.tpFreeVar)
                        {
                            throw new CompileError("Index should have integer type", p);
                        }
                        expr = new GetAtNode(type, tag, expr, index);
                        lex = scan();
                        continue;
					
                    default: 
                        return expr;
					
                }
            }
        }
		
        internal Node containsElement()
        {
            int p = pos;
            Node containsExpr = term();
            Type arrClass = containsExpr.Type;
#if WINRT_NET_FRAMEWORK
            if (arrClass == null || (!arrClass.IsArray && !arrClass.Equals(typeof(object)) && !(typeof(ICollection).GetTypeInfo().IsAssignableFrom(arrClass.GetTypeInfo()))))
#else
            if (arrClass == null || (!arrClass.IsArray && !arrClass.Equals(typeof(object)) && !(typeof(ICollection).IsAssignableFrom(arrClass))))
#endif
            {
                throw new CompileError("Contains clause can be applied only to arrays or collections", p);
            }
            Type arrElemType = arrClass.IsArray?arrClass.GetElementType():typeof(object);
            p = pos;
			
            ContainsNode outerContains = contains;
            ContainsNode innerContains = new ContainsNode(containsExpr, arrElemType);
            contains = innerContains;
			
            if (resolveMap != null)
            {
                ResolveMapping rm = (ResolveMapping) resolveMap[arrElemType];
                if (rm != null)
                {
                    innerContains.resolver = rm.resolver;
                    arrElemType = rm.resolved;
                }
            }
			
            if (lex == Token.tknWith)
            {
                innerContains.withExpr = checkType(NodeType.tpBool, disjunction());
            }
            if (lex == Token.tknGroup)
            {
                p = pos;
                if (scan() != Token.tknBy)
                {
                    throw new CompileError("GROUP BY expected", p);
                }
                p = pos;
                if (scan() != Token.tknIdent)
                {
                    throw new CompileError("GROUP BY field expected", p);
                }
                if (arrElemType.Equals(typeof(object)))
                {
                    innerContains.groupByFieldName = ident;
                }
                else
                {
                    FieldInfo groupByField = lookupField(arrElemType, ident);
                    if (groupByField == null)
                    {
                        MethodInfo groupByMethod = lookupMethod(arrElemType, ident, defaultProfile);
                        if (groupByMethod == null)
                        {
                            throw new CompileError("Field '" + ident + "' is not found", p);
                        }
                        innerContains.groupByMethod = groupByMethod;
                        Type rt = groupByMethod.ReturnType;
#if WINRT_NET_FRAMEWORK
                        if (rt.Equals(typeof(void)) || !(rt.GetTypeInfo().IsPrimitive && !typeof(IComparable).GetTypeInfo().IsAssignableFrom(rt.GetTypeInfo())))
#else
                        if (rt.Equals(typeof(void)) || !(rt.IsPrimitive && !typeof(IComparable).IsAssignableFrom(rt)))
#endif
                        {
                            throw new CompileError("Result type " + rt + " of sort method should be comparable", p);
                        }
                    }
                    else
                    {
                        Type type = groupByField.FieldType;
#if WINRT_NET_FRAMEWORK
                        if (!type.GetTypeInfo().IsPrimitive && !typeof(IComparable).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
#else
                        if (!type.IsPrimitive && !typeof(IComparable).IsAssignableFrom(type))
#endif
                        {
                            throw new CompileError("Order by field type " + type + " should be comparable", p);
                        }
                        innerContains.groupByField = groupByField;
                        innerContains.groupByType = Node.getNodeType(type);
                    }
                }
                if (scan() != Token.tknHaving)
                {
                    throw new CompileError("HAVING expected", pos);
                }
                innerContains.havingExpr = checkType(NodeType.tpBool, disjunction());
            }
            contains = outerContains;
            return innerContains;
        }
		
        internal Node aggregateFunction(Token cop)
        {
            int p = pos;
            AggregateFunctionNode agr;
            if (contains == null || (contains.groupByField == null && contains.groupByMethod == null && contains.groupByFieldName == null))
            {
                throw new CompileError("Aggregate function can be used only inside HAVING clause", p);
            }
            if (cop == Token.tknCount)
            {
                if (scan() != Token.tknLpar || scan() != Token.tknMul || scan() != Token.tknRpar)
                {
                    throw new CompileError("'count(*)' expected", p);
                }
                lex = scan();
                agr = new AggregateFunctionNode(NodeType.tpInt, NodeTag.opCount, null);
            }
            else
            {
                Node arg = term();
                if (arg.type == NodeType.tpAny)
                {
                    arg = new ConvertAnyNode(NodeType.tpReal, arg);
                }
                else if (arg.type != NodeType.tpInt && arg.type != NodeType.tpReal)
                {
                    throw new CompileError("Argument of aggregate function should have scalar type", p);
                }
                agr = new AggregateFunctionNode(arg.type, (NodeTag)((int)cop + (int)NodeTag.opAvg - (int)Token.tknAvg), arg);
            }
            agr.index = contains.aggregateFunctions.Count;
            contains.aggregateFunctions.Add(agr);
            return agr;
        }
		
        internal Node checkType(NodeType type, Node expr)
        {
            if (expr.type != type)
            {
                if (expr.type == NodeType.tpAny)
                {
                    expr = new ConvertAnyNode(type, expr);
                }
                else if (expr.type == NodeType.tpUnknown)
                {
                    expr.type = type;
                }
                else
                {
                    throw new CompileError(type.ToString() + " expression expected", pos);
                }
            }
            return expr;
        }
		
        internal Node term()
        {
            Token cop = scan();
            int p = pos;
            Node expr;
            Binding bp;
            switch (cop)
            {
				
                case Token.tknEof: 
                case Token.tknOrder: 
                    lex = cop;
                    return new EmptyNode();
				
                case Token.tknParam: 
                    expr = new ParameterNode(parameters);
                    break;
				
                case Token.tknIdent: 
                    for (bp = bindings; bp != null; bp = bp.next)
                    {
                        if (bp.name.Equals(ident))
                        {
                            lex = scan();
                            bp.used = true;
                            return new IndexNode(bp.loopId);
                        }
                    }
                    expr = component(null, cls);
                    return field(expr);
				
                case Token.tknContains: 
                    return containsElement();
				
                case Token.tknExists: 
                    if (scan() != Token.tknIdent)
                    {
                        throw new CompileError("Free variable name expected", p);
                    }
                    bindings = bp = new Binding(ident, vars++, bindings);
                    if (vars >= FilterIterator.maxIndexVars)
                    {
                        throw new CompileError("Too many nested EXISTS clauses", p);
                    }
                    p = pos;
                    if (scan() != Token.tknCol)
                    {
                        throw new CompileError("':' expected", p);
                    }
                    expr = checkType(NodeType.tpBool, term());
                    if (bp.used)
                    {
                        expr = new ExistsNode(expr, vars - 1);
                    }
                    vars -= 1;
                    bindings = bp.next;
                    return expr;
				
                case Token.tknCurrent: 
                    lex = scan();
                    return field(new CurrentNode(cls));
				
                case Token.tknFalse: 
                    expr = new ConstantNode(NodeType.tpBool, NodeTag.opFalse);
                    break;
				
                case Token.tknTrue: 
                    expr = new ConstantNode(NodeType.tpBool, NodeTag.opTrue);
                    break;
				
                case Token.tknNull: 
                    expr = new ConstantNode(NodeType.tpObj, NodeTag.opNull);
                    break;
				
                case Token.tknIconst: 
                    expr = new IntLiteralNode(ivalue);
                    break;
				
                case Token.tknFconst: 
                    expr = new RealLiteralNode(fvalue);
                    break;
				
                case Token.tknSconst: 
                    expr = new StrLiteralNode(svalue);
                    lex = scan();
                    return field(expr);
				
                case Token.tknSum: 
                case Token.tknMin: 
                case Token.tknMax: 
                case Token.tknAvg: 
                case Token.tknCount: 
                    return aggregateFunction(cop);
				
                case Token.tknSin: 
                case Token.tknCos: 
                case Token.tknTan: 
                case Token.tknAsin: 
                case Token.tknAcos: 
                case Token.tknAtan: 
                case Token.tknExp: 
                case Token.tknLog: 
                case Token.tknSqrt: 
                case Token.tknCeil: 
                case Token.tknFloor: 
                    expr = term();
                    if (expr.type == NodeType.tpInt)
                    {
                        expr = int2real(expr);
                    }
                    else if (expr.type == NodeType.tpAny)
                    {
                        expr = new ConvertAnyNode(NodeType.tpReal, expr);
                    }
                    else if (expr.type != NodeType.tpReal)
                    {
                        throw new CompileError("Numeric argument expected", p);
                    }
                    return new UnaryOpNode(NodeType.tpReal, (NodeTag)((int)cop + (int)NodeTag.opRealSin - (int)Token.tknSin), expr);
				
                case Token.tknAbs: 
                    expr = term();
                    if (expr.type == NodeType.tpInt)
                    {
                        return new UnaryOpNode(NodeType.tpInt, NodeTag.opIntAbs, expr);
                    }
                    else if (expr.type == NodeType.tpReal)
                    {
                        return new UnaryOpNode(NodeType.tpReal, NodeTag.opRealAbs, expr);
                    }
                    else if (expr.type == NodeType.tpAny)
                    {
                        return new UnaryOpNode(NodeType.tpAny, NodeTag.opAnyAbs, expr);
                    }
                    else
                    {
                        throw new CompileError("ABS function can be applied only to integer or real expression", p);
                    }
				
                case Token.tknLength: 
                    expr = term();
                    if (expr.type == NodeType.tpStr)
                    {
                        return new UnaryOpNode(NodeType.tpInt, NodeTag.opStrLength, expr);
                    }
                    else if (expr.type == NodeType.tpAny)
                    {
                        return new UnaryOpNode(NodeType.tpInt, NodeTag.opAnyLength, expr);
                    }
                    else if (expr.type >= NodeType.tpArrayBool)
                    {
                        return new UnaryOpNode(NodeType.tpInt, NodeTag.opLength, expr);
                    }
                    else
                    {
                        throw new CompileError("LENGTH function is defined only for arrays and strings", p);
                    }
   				
                case Token.tknLower: 
                    return field(new UnaryOpNode(NodeType.tpStr, NodeTag.opStrLower, checkType(NodeType.tpStr, term())));
				
                case Token.tknUpper: 
                    return field(new UnaryOpNode(NodeType.tpStr, NodeTag.opStrUpper, checkType(NodeType.tpStr, term())));
				
                case Token.tknInteger: 
                    return new UnaryOpNode(NodeType.tpInt, NodeTag.opRealToInt, checkType(NodeType.tpReal, term()));
				
                case Token.tknReal: 
                    return new UnaryOpNode(NodeType.tpInt, NodeTag.opIntToReal, checkType(NodeType.tpInt, term()));
				
                case Token.tknString: 
                    expr = term();
                    if (expr.type == NodeType.tpInt)
                    {
                        return field(new UnaryOpNode(NodeType.tpStr, NodeTag.opIntToStr, expr));
                    }
                    else if (expr.type == NodeType.tpReal)
                    {
                        return field(new UnaryOpNode(NodeType.tpStr, NodeTag.opRealToStr, expr));
                    }
                    else if (expr.type == NodeType.tpDate)
                    {
                        return field(new UnaryOpNode(NodeType.tpStr, NodeTag.opDateToStr, expr));
                    }
                    else if (expr.type == NodeType.tpAny)
                    {
                        return field(new UnaryOpNode(NodeType.tpStr, NodeTag.opAnyToStr, expr));
                    }
                    throw new CompileError("STRING function can be applied only to integer or real expression", p);
				
                case Token.tknLpar: 
                {
                    expr = disjunction();
                    Node list = null;
                    while (lex == Token.tknComma)
                    {
                        list = new BinOpNode(NodeType.tpList, NodeTag.opNop, list, expr);
                        expr = disjunction();
                    }
                    if (lex != Token.tknRpar)
                    {
                        throw new CompileError("')' expected", pos);
                    }
                    if (list != null)
                    {
                        expr = new BinOpNode(NodeType.tpList, NodeTag.opNop, list, expr);
                    } 
                    else 
                    { 
                        singleElementList = expr;
                    }
                    break;
                }
				
                case Token.tknNot: 
                    expr = comparison();
                    if (expr.type == NodeType.tpInt)
                    {
                        if (expr.tag == NodeTag.opIntConst)
                        {
                            IntLiteralNode ic = (IntLiteralNode) expr;
                            ic.val = ~ ic.val;
                        }
                        else
                        {
                            expr = new UnaryOpNode(NodeType.tpInt, NodeTag.opIntNot, expr);
                        }
                        return expr;
                    }
                    else if (expr.type == NodeType.tpBool)
                    {
                        return new UnaryOpNode(NodeType.tpBool, NodeTag.opBoolNot, expr);
                    }
                    else if (expr.type == NodeType.tpAny)
                    {
                        return new UnaryOpNode(NodeType.tpAny, NodeTag.opAnyNot, expr);
                    }
                    else
                    {
                        throw new CompileError("NOT operator can be applied only to integer or boolean expressions", p);
                    }
				
                case Token.tknAdd: 
                    throw new CompileError("Using of unary plus operator has no sense", p);
				
                case Token.tknSub: 
                    expr = term();
                    if (expr.type == NodeType.tpInt)
                    {
                        if (expr.tag == NodeTag.opIntConst)
                        {
                            IntLiteralNode ic = (IntLiteralNode) expr;
                            ic.val = - ic.val;
                        }
                        else
                        {
                            expr = new UnaryOpNode(NodeType.tpInt, NodeTag.opIntNeg, expr);
                        }
                    }
                    else if (expr.type == NodeType.tpReal)
                    {
                        if (expr.tag == NodeTag.opRealConst)
                        {
                            RealLiteralNode fc = (RealLiteralNode) expr;
                            fc.val = - fc.val;
                        }
                        else
                        {
                            expr = new UnaryOpNode(NodeType.tpReal, NodeTag.opRealNeg, expr);
                        }
                    }
                    else if (expr.type == NodeType.tpAny)
                    {
                        expr = new UnaryOpNode(NodeType.tpAny, NodeTag.opAnyNeg, expr);
                    }
                    else
                    {
                        throw new CompileError("Unary minus can be applied only to numeric expressions", p);
                    }
                    return expr;
				
                default: 
                    throw new CompileError("operand expected", p);
				
            }
            lex = scan();
            return expr;
        }
		
#if USE_GENERICS
        internal IEnumerable<T> filter(IEnumerable iterator, Node condition)
        {
            return new FilterIterator<T>(this, iterator, condition);
        }
#else		
        internal IEnumerable filter(IEnumerable iterator, Node condition)
        {
            return new FilterIterator(this, iterator, condition);
        }
#endif
		
        JoinIterator join(LoadNode deref, JoinIterator parent)
        {
            if (deref.baseExpr == null || deref.baseExpr.tag != NodeTag.opLoad) 
            { 
                return null;
            }
            deref = (LoadNode)deref.baseExpr;
            GenericIndex joinIndex = getIndex(deref.DeclaringType, deref.field.Name);
            if (joinIndex == null) 
            { 
                return null;
            }
            parent.joinIndex = joinIndex;
            if (deref.IsSelfField) 
            { 
                return parent;
            }
            JoinIterator child = new JoinIterator();
            child.iterator = parent;
            return join(deref, child);
        }       

        IEnumerable binOpIndex(GenericIndex index, BinOpNode cmp)
        {
            Key key;
            IterationOrder sort = IterationOrder.AscentOrder;
            if (order != null && order.field != null && order.field.Equals(cmp.left.Field) && !order.ascent) 
            { 
                sort = IterationOrder.DescentOrder;
            }     
            switch (cmp.tag)
            {						
                case NodeTag.opInAny:
                case NodeTag.opScanCollection:
                    return new UnionIterator(index, null, (IEnumerable)((LiteralNode)cmp.right).Value);
                case NodeTag.opObjEq: 
                case NodeTag.opAnyEq: 
                case NodeTag.opIntEq: 
                case NodeTag.opRealEq: 
                case NodeTag.opStrIgnoreCaseEq: 
                case NodeTag.opStrEq: 
                case NodeTag.opDateEq: 
                case NodeTag.opBoolEq: 
                    if ((key = keyLiteral(index.KeyType, cmp.right, true)) != null) 
                    {
                        return index.Range(key, key, sort);
                    }
                    break;
                case NodeTag.opIntGt: 
                case NodeTag.opRealGt: 
                case NodeTag.opStrGt: 
                case NodeTag.opStrIgnoreCaseGt: 
                case NodeTag.opDateGt: 
                case NodeTag.opAnyGt: 
                    if ((key = keyLiteral(index.KeyType, cmp.right, false)) != null) 
                    {
                        return index.Range(key, null, sort);
                    }
                    break;
                case NodeTag.opIntGe: 
                case NodeTag.opRealGe: 
                case NodeTag.opStrGe: 
                case NodeTag.opStrIgnoreCaseGe: 
                case NodeTag.opDateGe: 
                case NodeTag.opAnyGe: 
                    if ((key = keyLiteral(index.KeyType, cmp.right, true)) != null) 
                    {
                        return index.Range(key, null, sort);
                    }
                    break;			
                case NodeTag.opIntLt: 
                case NodeTag.opRealLt: 
                case NodeTag.opStrLt: 
                case NodeTag.opStrIgnoreCaseLt: 
                case NodeTag.opDateLt: 
                case NodeTag.opAnyLt: 
                    if ((key = keyLiteral(index.KeyType, cmp.right, false)) != null)
                    {
                        return index.Range(null, key, sort);
                    }
                    break;
                case NodeTag.opIntLe: 
                case NodeTag.opRealLe: 
                case NodeTag.opStrLe: 
                case NodeTag.opStrIgnoreCaseLe: 
                case NodeTag.opDateLe: 
                case NodeTag.opAnyLe: 
                    if ((key = keyLiteral(index.KeyType, cmp.right, true)) != null) 
                    { 
                        return index.Range(null, key, sort);
                    }
                    break;
            }
           return null;
        }

        IEnumerable tripleOpIndex(GenericIndex index, CompareNode cmp)
        {
            IterationOrder sort = IterationOrder.AscentOrder;
            if (order != null && order.field != null && order.field.Equals(cmp.o1.Field) && !order.ascent) 
            { 
                sort = IterationOrder.DescentOrder;
            }     
            switch (cmp.tag)
            {						
                case NodeTag.opIntBetween: 
                case NodeTag.opStrBetween: 
                case NodeTag.opStrIgnoreCaseBetween: 
                case NodeTag.opRealBetween: 
                case NodeTag.opDateBetween: 
                case NodeTag.opAnyBetween: 
                {
                    Key value1 = keyLiteral(index.KeyType, cmp.o2, true);
                    Key value2 = keyLiteral(index.KeyType, cmp.o3, true);
                    if (value1 != null && value2 != null)
                    {
                        return index.Range(value1, value2, sort);
                    }
                    break;
                }						
                case NodeTag.opStrLike: 
                case NodeTag.opStrLikeEsc: 
                case NodeTag.opStrIgnoreCaseLike: 
                case NodeTag.opStrIgnoreCaseLikeEsc: 
                {
                    string pattern = (string) ((LiteralNode)cmp.o2).Value;
                    char escape = cmp.o3 != null?((string) ((LiteralNode)cmp.o3).Value)[0]:'\\';
                    if (escape == '\\' && order == null && index is GenericRegexIndex)
                    {
                        return ((GenericRegexIndex)index).Match(pattern);
                    }
                    int pref = 0;
                    while (pref < pattern.Length)
                    {
                        char ch = pattern[pref];
                        if (ch == '%' || ch == '_')
                        {
                            break;
                        }
                        else if (ch == escape)
                        {
                            pref += 2;
                        }
                        else
                        {
                            pref += 1;
                        }
                    }
                    if (pref > 0)
                    {
                        if (pref == pattern.Length)
                        {
                            Key val = new Key(pattern);
                            return index.Range(val, val, sort);
                        } 
                        else 
                        {
                            return index.StartsWith(pattern.Substring(0, pref), sort);
                        }
                    }
                }
                break;
            }
            return null;
        }

        static bool isCaseInsensitive(Node node) 
        { 
            FieldInfo f = node.Field;
            if (f != null) { 
                foreach (IndexableAttribute idx in f.GetCustomAttributes(typeof(IndexableAttribute), false))
                { 
                    return idx.CaseInsensitive;
                }                   
            }
            return false;
        }

        static bool isPatternMatch(Node node) 
        { 
            switch (node.tag) {
            case NodeTag.opStrLike:
            case NodeTag.opStrLikeEsc:
            case NodeTag.opStrIgnoreCaseLike:
            case NodeTag.opStrIgnoreCaseLikeEsc:
                return true;
            default:
                return false;
            }
        }

        static bool isEqComparison(Node node) 
        { 
            switch (node.tag) {
            case NodeTag.opObjEq: 
            case NodeTag.opAnyEq: 
            case NodeTag.opIntEq: 
            case NodeTag.opRealEq: 
            case NodeTag.opStrEq: 
            case NodeTag.opStrIgnoreCaseEq: 
            case NodeTag.opDateEq: 
            case NodeTag.opBoolEq: 
                return true;
            default:
                return false;
            }
        }
        
        bool isUniqueIndex(Node node) 
        { 
            string key = node.FieldName;
            if (key != null) {
                GenericIndex index = getIndex(cls, key);  
                return index != null && index.IsUnique;
            }
            return false;
        }
    
        int getEqualsCost(Node node) 
        { 
            BinOpNode bin = (BinOpNode)node;
            int cost = Math.Max(bin.left.IndirectionLevel, bin.right.IndirectionLevel)*storage.sqlOptimizerParams.indirectionCost;
            if (!isUniqueIndex(bin.left) && !isUniqueIndex(bin.right)) { 
                cost += storage.sqlOptimizerParams.notUniqCost;
            }
            return cost;
        }

        int calculateCost(Node node) 
        {
            SqlOptimizerParameters p = storage.sqlOptimizerParams;
            switch (node.tag) 
            { 
            case NodeTag.opContains:
                return p.containsCost + ((ContainsNode)node).withExpr.IndirectionLevel*p.indirectionCost;
            case NodeTag.opBoolAnd:
                return p.andCost + Math.Min(calculateCost(((BinOpNode)node).left), calculateCost(((BinOpNode)node).right));
            case NodeTag.opBoolOr:
                return p.orCost + calculateCost(((BinOpNode)node).left) + calculateCost(((BinOpNode)node).right);
            case NodeTag.opBoolEq:
                return p.eqBoolCost + getEqualsCost(node);
            case NodeTag.opStrEq:
            case NodeTag.opStrIgnoreCaseEq:
                return p.eqStringCost + getEqualsCost(node);
            case NodeTag.opRealEq:
                return p.eqRealCost + getEqualsCost(node);
            case NodeTag.opStrIgnoreCaseGt:
            case NodeTag.opStrIgnoreCaseGe:
            case NodeTag.opStrIgnoreCaseLt:
            case NodeTag.opStrIgnoreCaseLe:
            case NodeTag.opStrGt:
            case NodeTag.opStrGe:
            case NodeTag.opStrLt:
            case NodeTag.opStrLe:
            case NodeTag.opIntGt:
            case NodeTag.opIntGe:
            case NodeTag.opIntLt:
            case NodeTag.opIntLe:
            case NodeTag.opRealGt:
            case NodeTag.opRealGe:
            case NodeTag.opRealLt:
            case NodeTag.opRealLe:
            case NodeTag.opDateGt:
            case NodeTag.opDateGe:
            case NodeTag.opDateLt:
            case NodeTag.opDateLe:
                return p.openIntervalCost + Math.Max(((BinOpNode)node).left.IndirectionLevel, 
                                                     ((BinOpNode)node).right.IndirectionLevel)*p.indirectionCost;
            case NodeTag.opStrBetween:
            case NodeTag.opStrIgnoreCaseBetween:
            case NodeTag.opRealBetween:
            case NodeTag.opIntBetween:
            case NodeTag.opDateBetween:
                return p.closeIntervalCost + ((CompareNode)node).o1.IndirectionLevel*p.indirectionCost;
            case NodeTag.opStrLike:
            case NodeTag.opStrLikeEsc:
            case NodeTag.opStrIgnoreCaseLike:
            case NodeTag.opStrIgnoreCaseLikeEsc:
                return p.patternMatchCost + ((CompareNode)node).o1.IndirectionLevel*p.indirectionCost;
            case NodeTag.opObjEq:
            case NodeTag.opIntEq:           
            case NodeTag.opDateEq:           
                return p.eqCost + getEqualsCost(node);
            case NodeTag.opIsNull:
                return p.isNullCost + ((UnaryOpNode)node).opd.IndirectionLevel*p.indirectionCost;
            case NodeTag.opBoolNot:
                return p.eqBoolCost + p.notUniqCost + ((UnaryOpNode)node).opd.IndirectionLevel*p.indirectionCost;
            case NodeTag.opLoad:
                return p.eqBoolCost + p.notUniqCost + node.IndirectionLevel*p.indirectionCost;
            default:
                return p.sequentialSearchCost;
            }
        }

#if USE_GENERICS
        internal IEnumerable<T> applyIndex(Node condition, Node predicate, Node filterCondition, out FieldInfo keyField)
        {
            IEnumerable<T> result;
#else
        internal IEnumerable applyIndex(Node condition, Node predicate, Node filterCondition, out FieldInfo keyField)
        {
            IEnumerable result;
#endif
            keyField = null;
            ArrayList alternatives = null;
            switch (condition.tag) { 
                case NodeTag.opBoolAnd:
                { 
                    BinOpNode and = (BinOpNode)condition;            
                    if (storage.sqlOptimizerParams.enableCostBasedOptimization && calculateCost(and.left) > calculateCost(and.right)) 
                    { 
                        result = applyIndex(and.right, predicate, filterCondition == null ? and.left : predicate, out keyField);
                        if (result != null) 
                        { 
                            return result;
                        }
                        return applyIndex(and.left, predicate, filterCondition == null ? and.right : predicate, out keyField);
                    } 
                    else 
                    {                   
                        result = applyIndex(and.left, predicate, filterCondition == null ? and.right : predicate, out keyField);
                        if (result != null) 
                        { 
                            return result;
                        }
                        return applyIndex(and.right, predicate, filterCondition == null ? and.left : predicate, out keyField);
                    }
                }      
                case NodeTag.opContains:
                {
                    ContainsNode contains = (ContainsNode) condition;
                    if (contains.withExpr == null)
                    {
                        return null;
                    }
                    if (filterCondition == null && contains.havingExpr != null) 
                    { 
                        filterCondition = condition;
                    }
                    return applyIndex(contains.withExpr, predicate, filterCondition, out keyField);
                }
                case NodeTag.opBoolOr:
                {
                     BinOpNode or = (BinOpNode)condition;
                     if (isEqComparison(or.left) && ((BinOpNode)or.left).right is LiteralNode) { 
                         NodeTag cop = or.left.tag;
                         Node baseExpr = ((BinOpNode)or.left).left;
                         Node right = or.right;                
                         condition = or.left;
                         alternatives = new ArrayList();
                         while (right is BinOpNode) { 
                             Node cmp;
                             if (right.tag == NodeTag.opBoolOr) { 
                                 or = (BinOpNode)right;
                                 right = or.right;
                                 cmp = or.left;
                             } else { 
                                 cmp = right;
                                 right = null;
                             }
                             if (cmp.tag != cop
                                 || !baseExpr.Equals(((BinOpNode)cmp).left) 
                                 || !(((BinOpNode)cmp).right is LiteralNode))
                             {
                                 return null;
                             }
                             alternatives.Add(((LiteralNode)((BinOpNode)cmp).right).Value);
                         }
                         if (right != null) { 
                             return null;
                         }
                     }                        
                     break; 
                }
                case NodeTag.opIsNull:
                {
                    UnaryOpNode unary = (UnaryOpNode)condition;
                    String key = unary.opd.FieldName;
                    if (key != null) {
                        GenericIndex index = getIndex(cls, key);  
                        Key nil = new Key((IPersistent)null);
                        if (index == null) { 
                            if (unary.opd.tag == NodeTag.opLoad) { 
                                LoadNode deref = (LoadNode)unary.opd;
                                index = getIndex(deref.DeclaringType, deref.field.Name);
                                if (index != null) { 
                                    JoinIterator lastJoinIterator = new JoinIterator();
                                    JoinIterator firstJoinIterator = join(deref, lastJoinIterator);
                                    if (firstJoinIterator != null) { 
                                        IEnumerable iterator = index.Range(nil, nil, IterationOrder.AscentOrder);
                                        if (iterator != null) { 
                                            lastJoinIterator.iterator = iterator.GetEnumerator();
                                            return filter(firstJoinIterator, filterCondition);
                                        }
                                    }
                                }
                            }
                        } else { 
                            IEnumerable iterator = index.Range(nil, nil, IterationOrder.AscentOrder);
                            if (iterator != null) {  
                                keyField = unary.opd.Field;
                                return filter(iterator, filterCondition);
                            }
                        }
                    }
                    return null;
                }
                case NodeTag.opBoolNot:
                {
                    UnaryOpNode unary = (UnaryOpNode)condition;
                    if (unary.opd.tag == NodeTag.opLoad) { 
                        String key = unary.opd.FieldName;
                        if (key != null) {
                            GenericIndex index = getIndex(cls, key);  
                            Key f = new Key(false);
                            if (index == null) { 
                                LoadNode deref = (LoadNode)unary.opd;
                                index = getIndex(deref.DeclaringType, deref.field.Name);
                                if (index != null) { 
                                    JoinIterator lastJoinIterator = new JoinIterator();
                                    JoinIterator firstJoinIterator = join(deref, lastJoinIterator);
                                    if (firstJoinIterator != null) { 
                                        IEnumerable iterator = index.Range(f, f, IterationOrder.AscentOrder);
                                        if (iterator != null) { 
                                            lastJoinIterator.iterator = iterator.GetEnumerator();
                                            return filter(firstJoinIterator, filterCondition);
                                        }
                                    }
                                }
                            } else { 
                                IEnumerable iterator = index.Range(f, f, IterationOrder.AscentOrder);
                                if (iterator != null) {  
                                    keyField = unary.opd.Field;
                                    return filter(iterator, filterCondition);
                                }
                            }
                        }
                    }
                    return null;
                }
                case NodeTag.opLoad:
                {
                    String key = condition.FieldName;
                    if (key != null) {
                        GenericIndex index = getIndex(cls, key);  
                        Key t = new Key(true);
                        if (index == null) { 
                            LoadNode deref = (LoadNode)condition;
                            index = getIndex(deref.DeclaringType, deref.field.Name);
                            if (index != null) { 
                                JoinIterator lastJoinIterator = new JoinIterator();
                                JoinIterator firstJoinIterator = join(deref, lastJoinIterator);
                                if (firstJoinIterator != null) { 
                                    IEnumerable iterator = index.Range(t, t, IterationOrder.AscentOrder);
                                    if (iterator != null) { 
                                        lastJoinIterator.iterator = iterator.GetEnumerator();
                                        return filter(firstJoinIterator, filterCondition);
                                    }
                                }
                            }
                        } else { 
                            IEnumerable iterator = index.Range(t, t, IterationOrder.AscentOrder);
                            if (iterator != null) {  
                                keyField = condition.Field;
                                return filter(iterator, filterCondition);
                            }
                        }
                    }
                    return null;
                }
            }        
            if (condition is BinOpNode)
            {
                BinOpNode cmp = (BinOpNode) condition;
                string key = cmp.left.FieldName;
                if (key != null && cmp.right is LiteralNode)
                {
                    GenericIndex index = getIndex(cls, key);
                    if (index == null) 
                    { 
                        if (cmp.left.tag == NodeTag.opLoad) 
                        { 
                            LoadNode deref = (LoadNode)cmp.left;
                            index = getIndex(deref.DeclaringType, deref.field.Name);
                            if (index != null) 
                            { 
                                JoinIterator lastJoinIterator = new JoinIterator();
                                JoinIterator firstJoinIterator = join(deref, lastJoinIterator);
                                if (firstJoinIterator != null) 
                                { 
                                    IEnumerable iterator = binOpIndex(index, cmp);
                                    if (iterator != null) 
                                    { 
                                        if (alternatives != null) 
                                        { 
                                            iterator = new UnionIterator(index, iterator.GetEnumerator(), alternatives);
                                        }
                                        lastJoinIterator.iterator = iterator.GetEnumerator();
                                        return filter(firstJoinIterator, filterCondition);
                                    }
                                }
                            }
                        }
                    }   
                    else 
                    { 
                        IEnumerable iterator = binOpIndex(index, cmp);
                        if (iterator != null) 
                        { 
                            if (alternatives != null) 
                            { 
                                iterator = new UnionIterator(index, iterator.GetEnumerator(), alternatives);
                            } 
                            else if (!(iterator is UnionIterator))
                            { 
                                keyField = cmp.left.Field;
                            }
                            return filter(iterator, filterCondition);
                        }
                    }
                }
            }
            else if (condition is CompareNode)
            {
                CompareNode cmp = (CompareNode) condition;
                string key = cmp.o1.FieldName;
                if (key != null && cmp.o2 is LiteralNode && (cmp.o3 == null || cmp.o3 is LiteralNode))
                {
                    GenericIndex index = getIndex(cls, key);
                    if (index == null) 
                    { 
                        if (cmp.o1.tag == NodeTag.opLoad) 
                        { 
                            LoadNode deref = (LoadNode)cmp.o1;
                            index = getIndex(deref.DeclaringType, deref.field.Name);
                            if (index != null) 
                            { 
                                JoinIterator lastJoinIterator = new JoinIterator();
                                JoinIterator firstJoinIterator = join(deref, lastJoinIterator);
                                if (firstJoinIterator != null) 
                                { 
                                    IEnumerable iterator = tripleOpIndex(index, cmp);
                                    if (iterator != null) 
                                    { 
                                        lastJoinIterator.iterator = iterator.GetEnumerator();
                                        return filter(firstJoinIterator, 
                                            (filterCondition == null && isPatternMatch(cmp))
                                             ? condition : filterCondition);
                                    }
                                }
                            }
                        }
                    } 
                    else 
                    { 
                        IEnumerable iterator = tripleOpIndex(index, cmp); 
                        if (iterator != null) 
                        { 
                            keyField = cmp.o1.Field;
                            return filter(iterator, 
                                          (filterCondition == null && isPatternMatch(cmp))
                                           ? condition : filterCondition);
                        }
                    }
                }
            }
            return null;
        }
		
        internal void  compile()
        {
            pos = 0;
            vars = 0;
            Node predicate = checkType(NodeType.tpBool, disjunction());
            if (predicate.tag != NodeTag.opTrue) 
            { 
                tree = predicate;
            }
            OrderNode last = null;
            order = null;
            if (lex == Token.tknEof)
            {
                return ;
            }
            if (lex != Token.tknOrder)
            {
                throw new CompileError("ORDER BY expected", pos);
            }
            Token tkn;
            int p = pos;
            if (scan() != Token.tknBy)
            {
                throw new CompileError("BY expected after ORDER", p);
            }
            do 
            {
                p = pos;
                OrderNode node;
                Node orderExpr = disjunction();
                if (orderExpr.tag == NodeTag.opLoad && ((LoadNode)orderExpr).baseExpr == null) 
                { 
                    node = new OrderNode(orderExpr.Field);
                } 
                else if (orderExpr.tag == NodeTag.opInvoke && ((InvokeNode)orderExpr).target == null) 
                { 
                    node = new OrderNode(((InvokeNode)orderExpr).mth);
                } 
                else 
                { 
                    node = new OrderNode(orderExpr);
                }
                if (last != null)
                {
                    last.next = node;
                }
                else
                {
                    order = node;
                }
                last = node;
                p = pos;
                tkn = lex;
                if (tkn == Token.tknDesc)
                {
                    node.ascent = false;
                    tkn = scan();
                }
                else if (tkn == Token.tknAsc)
                {
                    tkn = scan();
                }
            }
            while (tkn == Token.tknComma);
            if (tkn != Token.tknEof)
            {
                throw new CompileError("',' expected", p);
            }
        }

        public QueryImpl(Storage storage)
        {
            this.storage = (StorageImpl) storage;
            parameters = new ArrayList();
            runtimeErrorsReporting = true;
        }				


        static QueryImpl()
        {
            defaultProfile = new Type[0];
            noArguments = new Node[0];
            symtab = new Hashtable();
            symtab["abs"] = new Symbol(Token.tknAbs);
            symtab["acos"] = new Symbol(Token.tknAcos);
            symtab["and"] = new Symbol(Token.tknAnd);
            symtab["asc"] =  new Symbol(Token.tknAsc);
            symtab["asin"] = new Symbol(Token.tknAsin);
            symtab["atan"] = new Symbol(Token.tknAtan);
            symtab["between"] = new Symbol(Token.tknBetween);
            symtab["by"] = new Symbol(Token.tknBy);
            symtab["ceal"] = new Symbol(Token.tknCeil);
            symtab["cos"] = new Symbol(Token.tknCos);
            symtab["current"] = new Symbol(Token.tknCurrent);
            symtab["desc"] = new Symbol(Token.tknDesc);
            symtab["escape"] = new Symbol(Token.tknEscape);
            symtab["exists"] = new Symbol(Token.tknExists);
            symtab["exp"] = new Symbol(Token.tknExp);
            symtab["false"] = new Symbol(Token.tknFalse);
            symtab["floor"] = new Symbol(Token.tknFloor);
            symtab["in"] = new Symbol(Token.tknIn);
            symtab["is"] = new Symbol(Token.tknIs);
            symtab["integer"] = new Symbol(Token.tknInteger);
            symtab["length"] = new Symbol(Token.tknLength);
            symtab["like"] = new Symbol(Token.tknLike);
            symtab["log"] = new Symbol(Token.tknLog);
            symtab["lower"] = new Symbol(Token.tknLower);
            symtab["not"] = new Symbol(Token.tknNot);
            symtab["null"] = new Symbol(Token.tknNull);
            symtab["or"] = new Symbol(Token.tknOr);
            symtab["order"] = new Symbol(Token.tknOrder);
            symtab["real"] = new Symbol(Token.tknReal);
            symtab["sin"] = new Symbol(Token.tknSin);
            symtab["sqrt"] = new Symbol(Token.tknSqrt);
            symtab["string"] = new Symbol(Token.tknString);
            symtab["true"] = new Symbol(Token.tknTrue);
            symtab["upper"] = new Symbol(Token.tknUpper);
            symtab["having"] = new Symbol(Token.tknHaving);
            symtab["contains"] = new Symbol(Token.tknContains);
            symtab["group"] = new Symbol(Token.tknGroup);
            symtab["min"] = new Symbol(Token.tknMin);
            symtab["max"] = new Symbol(Token.tknMax);
            symtab["count"] = new Symbol(Token.tknCount);
            symtab["avg"] = new Symbol(Token.tknAvg);
            symtab["sum"] = new Symbol(Token.tknSum);
            symtab["with"] = new Symbol(Token.tknWith);
        }
    }
}