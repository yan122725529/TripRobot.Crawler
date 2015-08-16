namespace Perst.Impl
{
    using System;
    using System.Collections;
    using System.Reflection;

    class CodeGeneratorImpl : CodeGenerator
    {
        QueryImpl query;
        Type cls;
    
        internal CodeGeneratorImpl(QueryImpl query, Type cls) { 
            if (cls == null) { 
                throw new CodeGeneratorException("No class defined");
            }
            this.cls = cls;
            this.query = query;
        }
        
        public Code Current() {
            return new CurrentNode(cls);
        }
    
        public Code Literal(object value) {
            if (value is long) { 
                return new IntLiteralNode((long)value);
            } else if (value is int) { 
                return new IntLiteralNode((int)value);
            } else if (value is uint) { 
                return new IntLiteralNode((uint)value);
            } else if (value is float) { 
                return new RealLiteralNode((float)value);
            } else if (value is double) { 
                return new RealLiteralNode((double)value);
            } else if (value is string) { 
                return new StrLiteralNode((string)value);
            } else if (value == null) { 
                return new ConstantNode(NodeType.tpObj, NodeTag.opNull);
            } else if (value is bool) { 
                return new ConstantNode(NodeType.tpBool, (bool)value ? NodeTag.opTrue : NodeTag.opFalse);
            } else if (value is DateTime) {
                return new DateLiteralNode((DateTime)value);
            } else { 
                throw new CodeGeneratorException("Not suppored literal type: " + value);
            }
        }
    
        public Code List(params Code[] values) {
            Node list = null;
            for (int i = 0; i < values.Length; i++) { 
                list = new BinOpNode(NodeType.tpList, NodeTag.opNop, list, (Node)values[i]);
            }
            return list;
        }
    
        public Code Parameter(int n, Type type) {
            NodeType paramType;
            if (n < 1) { 
                throw new CodeGeneratorException("Parameter index should be positive number");            
            }
            if (type == typeof(int) || type == typeof(long)) {
                paramType = NodeType.tpInt;
            } else if (type == typeof(float) || type == typeof(double)) { 
                paramType = NodeType.tpReal;
            } else if (type == typeof(string)) { 
                paramType = NodeType.tpStr;
            } else if (type == typeof(DateTime)) { 
                paramType = NodeType.tpDate;
#if WINRT_NET_FRAMEWORK
            } else if (typeof(ICollection).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo())) { 
#else
            } else if (typeof(ICollection).IsAssignableFrom(type)) { 
#endif
                paramType = NodeType.tpCollection;
            } else { 
                paramType = NodeType.tpObj;
            }
            return new ParameterNode(query.parameters, n-1, paramType);
        }
    
    
        public  Code Field(String name) {
            return Field(null, name);
        }
    
        public Code Field(Code baseExpr, String name) {
            Type scope = (baseExpr == null) ? cls : ((Node)baseExpr).Type;
            FieldInfo f = QueryImpl.lookupField(scope, name);                    
            if (f == null) {             
                throw new CodeGeneratorException("No such field " + name + " in class " + scope);
            }
            return new LoadNode((Node)baseExpr, f);
        }
    
        public Code Invoke(Code baseExpr, String name, params Code[] arguments) {
            Type[] profile = new Type[arguments.Length];
            Node[] args = new Node[arguments.Length];
            for (int i = 0; i < profile.Length; i++) { 
                Node arg = (Node)arguments[i];
                args[i] = arg;
                Type argType;
                switch (arg.type) {
                case NodeType.tpInt:
                    argType = typeof(long);
                    break;
                case NodeType.tpReal:
                    argType = typeof(double);
                    break;
                case NodeType.tpStr:
                    argType = typeof(String);
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
                    argType = typeof(byte[]);
                    break;
                case NodeType.tpArrayUInt2:
                    argType = typeof(ushort[]);
                    break;
                case NodeType.tpArrayUInt4:
                    argType = typeof(uint[]);
                    break;
                case NodeType.tpArrayUInt8:
                    argType = typeof(ulong[]);
                    break;
                case NodeType.tpArrayReal4:
                    argType = typeof(float[]);
                    break;
                case NodeType.tpArrayReal8:
                    argType = typeof(double[]);
                    break;
                case NodeType.tpArrayStr:
                    argType = typeof(String[]);
                    break;
                case NodeType.tpArrayObj:
                    argType = typeof(Object[]);
                    break;
                default:
                    throw new CodeGeneratorException("Invalid method argument type");
                }
                profile[i] = argType;
            }
            Type scope = (baseExpr == null) ? cls : ((Node)baseExpr).Type;
            MethodInfo mth = QueryImpl.lookupMethod(scope, name, profile);
            if (mth == null) { 
                throw new CodeGeneratorException("MethodInfo " + name + " not found in class " + scope);
            }            
            return new InvokeNode((Node)baseExpr, mth, args);
        }
    
        public Code Invoke(String name, params Code[] arguments) {
            return Invoke(null, name, arguments);
        }
    
    
        public Code And(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntAnd, left, right);
            } else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opBoolAnd, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Or(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntOr, left, right);
            } else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opBoolOr, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
    
        public Code Add(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpReal, NodeTag.opRealAdd, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntAdd, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpStr, NodeTag.opStrConcat, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Sub(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpReal, NodeTag.opRealSub, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntSub, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Mul(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpReal, NodeTag.opRealMul, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntMul, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Div(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpReal, NodeTag.opRealDiv, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntDiv, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Pow(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpReal, NodeTag.opRealPow, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpInt, NodeTag.opIntPow, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Like(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type != NodeType.tpStr || right.type != NodeType.tpStr) { 
                throw new CodeGeneratorException("Invalid argument types");
            }
            return new CompareNode(NodeTag.opStrLike, left, right, null);
        }
    
        public Code Like(Code opd1, Code opd2, Code opd3) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            Node esc = (Node)opd3;
            if (left.type != NodeType.tpStr || right.type != NodeType.tpStr || esc.tag != NodeTag.opStrConst) { 
                throw new CodeGeneratorException("Invalid argument types");
            }
            return new CompareNode(NodeTag.opStrLikeEsc, left, right, esc);
        }
    
    
        public Code Eq(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opRealEq, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opIntEq, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opStrEq, left, right);
            } else if (left.type == NodeType.tpDate || right.type == NodeType.tpDate) {
                if (left.type == NodeType.tpStr) {
                    left = QueryImpl.str2date(left);
                } else if (left.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpStr) {
                    right = QueryImpl.str2date(right);
                } else if (right.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opDateEq, left, right);
            } else if (left.type == NodeType.tpObj && right.type == NodeType.tpObj) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opObjEq, left, right);
            } else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opBoolEq, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Ge(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opRealGe, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opIntGe, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opStrGe, left, right);
            } else if (left.type == NodeType.tpDate || right.type == NodeType.tpDate) {
                if (left.type == NodeType.tpStr) {
                    left = QueryImpl.str2date(left);
                } else if (left.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpStr) {
                    right = QueryImpl.str2date(right);
                } else if (right.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opDateGe, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Gt(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opRealGt, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opIntGt, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opStrGt, left, right);
            } else if (left.type == NodeType.tpDate || right.type == NodeType.tpDate) {
                if (left.type == NodeType.tpStr) {
                    left = QueryImpl.str2date(left);
                } else if (left.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpStr) {
                    right = QueryImpl.str2date(right);
                } else if (right.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opDateGt, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Lt(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opRealLt, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opIntLt, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opStrLt, left, right);
            } else if (left.type == NodeType.tpDate || right.type == NodeType.tpDate) {
                if (left.type == NodeType.tpStr) {
                    left = QueryImpl.str2date(left);
                } else if (left.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpStr) {
                    right = QueryImpl.str2date(right);
                } else if (right.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opDateLt, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Le(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opRealLe, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opIntLe, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opStrLe, left, right);
            } else if (left.type == NodeType.tpDate || right.type == NodeType.tpDate) {
                if (left.type == NodeType.tpStr) {
                    left = QueryImpl.str2date(left);
                } else if (left.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpStr) {
                    right = QueryImpl.str2date(right);
                } else if (right.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opDateLe, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code Ne(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (left.type == NodeType.tpReal || right.type == NodeType.tpReal) { 
                if (left.type == NodeType.tpInt) { 
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpInt) { 
                    right = QueryImpl.int2real(right);
                } else if (right.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opRealNe, left, right);
            } else if (left.type == NodeType.tpInt && right.type == NodeType.tpInt) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opIntNe, left, right);
            } else if (left.type == NodeType.tpStr && right.type == NodeType.tpStr) {
                return new BinOpNode(NodeType.tpBool, NodeTag.opStrNe, left, right);
            } else if (left.type == NodeType.tpDate || right.type == NodeType.tpDate) {
                if (left.type == NodeType.tpStr) {
                    left = QueryImpl.str2date(left);
                } else if (left.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (right.type == NodeType.tpStr) {
                    right = QueryImpl.str2date(right);
                } else if (right.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opDateNe, left, right);
            } else if (left.type == NodeType.tpObj && right.type == NodeType.tpObj) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opObjNe, left, right);
            } else if (left.type == NodeType.tpBool && right.type == NodeType.tpBool) { 
                return new BinOpNode(NodeType.tpBool, NodeTag.opBoolNe, left, right);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
    
        public Code Neg(Code opd) {
            Node expr = (Node)opd;
            if (expr.type == NodeType.tpInt) { 
                if (expr.tag == NodeTag.opIntConst) { 
                    IntLiteralNode ic = (IntLiteralNode)expr;
                    ic.val = -ic.val;
                } else {
                    expr = new UnaryOpNode(NodeType.tpInt, NodeTag.opIntNeg, expr);
                } 
            } else if (expr.type == NodeType.tpReal) { 
                if (expr.tag == NodeTag.opRealConst) { 
                    RealLiteralNode fc = (RealLiteralNode)expr;
                    fc.val = -fc.val;
                } else {
                    expr = new UnaryOpNode(NodeType.tpReal, NodeTag.opRealNeg, expr);
                } 
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }        
            return expr;
        }
    
        public Code Abs(Code opd) {
            Node expr = (Node)opd;
            if (expr.type == NodeType.tpInt) { 
                return new UnaryOpNode(NodeType.tpInt, NodeTag.opIntAbs, expr);
            } else if (expr.type == NodeType.tpReal) { 
                return new UnaryOpNode(NodeType.tpReal, NodeTag.opRealAbs, expr);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }        
        }
    
        public Code Not(Code opd) {
            Node expr = (Node)opd;
            if (expr.type == NodeType.tpInt) { 
                if (expr.tag == NodeTag.opIntConst) { 
                    IntLiteralNode ic = (IntLiteralNode)expr;
                    ic.val = ~ic.val;
                } else {
                    expr = new UnaryOpNode(NodeType.tpInt, NodeTag.opIntNot, expr);
                } 
                return expr;
            } else if (expr.type == NodeType.tpBool) { 
                return new UnaryOpNode(NodeType.tpBool, NodeTag.opBoolNot, expr);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
    
        public Code Between(Code opd1, Code opd2, Code opd3) {
            Node expr = (Node)opd1;
            Node low  = (Node)opd2;
            Node high = (Node)opd3;
            if (expr.type == NodeType.tpReal || low.type == NodeType.tpReal || high.type == NodeType.tpReal) {
                if (expr.type == NodeType.tpInt) { 
                    expr = QueryImpl.int2real(expr);
                } else if (expr.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (low.type == NodeType.tpInt) {
                    low = QueryImpl.int2real(low);
                } else if (low.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (high.type == NodeType.tpInt) {
                    high = QueryImpl.int2real(high);
                } else if (high.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new CompareNode(NodeTag.opRealBetween, expr, low, high);
            } else if (expr.type == NodeType.tpInt && low.type == NodeType.tpInt && high.type == NodeType.tpInt) {                   
                return new CompareNode(NodeTag.opIntBetween, expr, low, high);
            } else if (expr.type == NodeType.tpStr && low.type == NodeType.tpStr && high.type == NodeType.tpStr) {
                return new CompareNode(NodeTag.opStrBetween, expr, low, high);
            } else if (expr.type == NodeType.tpDate) { 
                if (low.type == NodeType.tpStr) {
                    low = QueryImpl.str2date(low);
                } else if (low.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                if (high.type == NodeType.tpStr) {
                    high = QueryImpl.str2date(high);
                } else if (high.type != NodeType.tpDate) {
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new CompareNode(NodeTag.opDateBetween, expr, low, high);
            } else {         
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        private static Node listToTree(Node expr, BinOpNode list)
        {
            BinOpNode tree = null; 
            do { 
                Node elem = list.right;
                NodeTag cop = NodeTag.opNop;
                if (elem.type == NodeType.tpUnknown) { 
                    elem.type = expr.type;
                }
                if (expr.type == NodeType.tpInt) { 
                    if (elem.type == NodeType.tpReal) { 
                        expr = new UnaryOpNode(NodeType.tpReal, NodeTag.opIntToReal, expr);
                        cop = NodeTag.opRealEq;
                    } else if (elem.type == NodeType.tpInt) { 
                        cop = NodeTag.opIntEq;
                    }
                } else if (expr.type == NodeType.tpReal) {
                    if (elem.type == NodeType.tpReal) { 
                        cop = NodeTag.opRealEq;
                    } else if (elem.type == NodeType.tpInt) { 
                        cop = NodeTag.opRealEq;
                        elem = QueryImpl.int2real(elem);
                    }
                } else if (expr.type == NodeType.tpDate && elem.type == NodeType.tpDate) {
                    cop = NodeTag.opDateEq;
                } else if (expr.type == NodeType.tpStr && elem.type == NodeType.tpStr) {
                    cop = NodeTag.opStrEq;
                } else if (expr.type == NodeType.tpObj && elem.type == NodeType.tpObj) {
                    cop = NodeTag.opObjEq;
                } else if (expr.type == NodeType.tpBool && elem.type == NodeType.tpBool) {
                    cop = NodeTag.opBoolEq;
                }
                if (cop == NodeTag.opNop) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                BinOpNode cmp = new BinOpNode(NodeType.tpBool, cop, expr, elem);
                if (tree == null) { 
                    tree = cmp; 
                } else {
                    tree = new BinOpNode(NodeType.tpBool, NodeTag.opBoolOr, cmp, tree);
                }
            } while ((list = (BinOpNode)list.left) != null);
            return tree;
        }
    
        public Code In(Code opd1, Code opd2) {
            Node left = (Node)opd1;
            Node right = (Node)opd2;
            if (right == null) 
            {
                return new ConstantNode(NodeType.tpBool, NodeTag.opFalse);
            }
            switch (right.type) {
            case NodeType.tpCollection:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanCollection, left, right);
            case NodeType.tpArrayBool:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayBool, checkType(NodeType.tpBool, left), right);
            case NodeType.tpArrayChar:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayChar, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayInt1:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt1, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayInt2:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt2, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayInt4:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt4, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayInt8:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayInt8, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayUInt1:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayUInt1, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayUInt2:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayUInt2, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayUInt4:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayUInt4, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayUInt8:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayUInt8, checkType(NodeType.tpInt, left), right);
            case NodeType.tpArrayReal4:
                if (left.type == NodeType.tpInt) {
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayReal4, left, right);
            case NodeType.tpArrayReal8:
                if (left.type == NodeType.tpInt) {
                    left = QueryImpl.int2real(left);
                } else if (left.type != NodeType.tpReal) { 
                    throw new CodeGeneratorException("Invalid argument types");
                }
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayReal8, left, right);
            case NodeType.tpArrayObj:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayObj, checkType(NodeType.tpObj, left), right);
            case NodeType.tpArrayStr:
                return new BinOpNode(NodeType.tpBool, NodeTag.opScanArrayStr, checkType(NodeType.tpStr, left), right);
            case NodeType.tpStr:
                return new BinOpNode(NodeType.tpBool, NodeTag.opInString, checkType(NodeType.tpStr, left), right);
            case NodeType.tpList:
                return listToTree(left, (BinOpNode)right);
            default:
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        Node mathFunc(NodeTag cop, Code opd) { 
            Node expr = (Node)opd;
            if (expr.type == NodeType.tpInt) { 
                expr = QueryImpl.int2real(expr);
            } else if (expr.type != NodeType.tpReal) { 
                throw new CodeGeneratorException("Invalid argument types");
            }
            return new UnaryOpNode(NodeType.tpReal, cop, expr);
        }
    
        public Code Sin(Code opd) {
            return mathFunc(NodeTag.opRealSin, opd);
        }
    
        public Code Cos(Code opd) {
            return mathFunc(NodeTag.opRealCos, opd);
        }
    
        public Code Tan(Code opd) {
            return mathFunc(NodeTag.opRealTan, opd);
        }
    
        public Code Asin(Code opd) {
            return mathFunc(NodeTag.opRealAsin, opd);
        }
    
        public Code Acos(Code opd) {
            return mathFunc(NodeTag.opRealAcos, opd);
        }
    
        public Code Atan(Code opd) {
            return mathFunc(NodeTag.opRealAtan, opd);
        }
    
        public Code Sqrt(Code opd) {
            return mathFunc(NodeTag.opRealSqrt, opd);
        }
    
        public Code Exp(Code opd) {
            return mathFunc(NodeTag.opRealExp, opd);
        }
    
        public Code Log(Code opd) {
            return mathFunc(NodeTag.opRealLog, opd);
        }
    
        public Code Ceil(Code opd) {
            return mathFunc(NodeTag.opRealCeil, opd);
        }
    
        public Code Floor(Code opd) {
            return mathFunc(NodeTag.opRealFloor, opd);
        }
    
        private static Node checkType(NodeType type, Code opd) {
            Node expr = (Node)opd;
            if (expr.type != type) { 
                throw new CodeGeneratorException("Invalid argument types");
            }
            return expr;
        }
    
        public Code Upper(Code opd) {
            return new UnaryOpNode(NodeType.tpStr, NodeTag.opStrUpper, checkType(NodeType.tpStr, opd));
        }
    
        public Code Lower(Code opd) {
            return new UnaryOpNode(NodeType.tpStr, NodeTag.opStrLower, checkType(NodeType.tpStr, opd));
        }
    
        public Code Length(Code opd) {
            Node expr = (Node)opd;
            if (expr.type == NodeType.tpStr) { 
                return new UnaryOpNode(NodeType.tpInt, NodeTag.opStrLength, expr);
            } else if (expr.type >= NodeType.tpArrayBool && expr.type <= NodeType.tpArrayObj) { 
                return new UnaryOpNode(NodeType.tpInt, NodeTag.opLength, expr);
            } else { 
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code String(Code opd) {
            Node expr = (Node)opd;
            if (expr.type == NodeType.tpInt) { 
                return new UnaryOpNode(NodeType.tpStr, NodeTag.opIntToStr, expr);
            } else if (expr.type == NodeType.tpReal) { 
                return new UnaryOpNode(NodeType.tpStr, NodeTag.opRealToStr, expr);
            } else if (expr.type == NodeType.tpDate) { 
                return new UnaryOpNode(NodeType.tpStr, NodeTag.opDateToStr, expr);
            } else {
                throw new CodeGeneratorException("Invalid argument types");
            }
        }
    
        public Code GetAt(Code opd1, Code opd2) {
            Node expr = (Node)opd1;
            Node index = checkType(NodeType.tpInt, opd2);
            NodeTag tag;
            NodeType type;
            switch (expr.type) { 
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
            default: 
                throw new CodeGeneratorException("Invalid argument types");
            }
            return new GetAtNode(type, tag, expr, index);                
        }
    
        public Code Integer(Code opd) {
            return new UnaryOpNode(NodeType.tpStr, NodeTag.opStrLower, checkType(NodeType.tpReal, opd));
        }
    
        public Code Real(Code opd) {
            return new UnaryOpNode(NodeType.tpStr, NodeTag.opStrLower, checkType(NodeType.tpReal, opd));
        }
    
        public void Predicate(Code code) { 
            query.tree = checkType(NodeType.tpBool, code);
        }
    
        public void OrderBy(String name, bool ascent) { 
            FieldInfo f = QueryImpl.lookupField(cls, name);
            OrderNode node;
            if (f == null) {
                MethodInfo m = QueryImpl.lookupMethod(cls, name, QueryImpl.defaultProfile);
                if (m == null) { 
                    throw new CodeGeneratorException("No such field " + name + " in class " + cls);
                } else { 
                    node = new OrderNode(m);
                }
            } else {
                node = new OrderNode(f);
            }
            node.ascent = ascent;
            if (query.order == null) { 
                query.order = node;
            } else { 
                OrderNode last;
                for (last = query.order; last.next != null; last = last.next);
                last.next = node;
            }
        }
        
        public void OrderBy(String name) { 
            OrderBy(name, true);
        }     
    }
}