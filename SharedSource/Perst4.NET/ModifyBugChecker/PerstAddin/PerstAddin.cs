using System;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Text;

using Reflector;
using Reflector.CodeModel;

namespace Perst
{
    internal class PerstAddin : IPackage
    {
        IWindowManager windowManager;
        IAssemblyManager assemblyManager;
        ICommandBarManager commandBarManager;
        ICommandBarSeparator separator;
        ICommandBarButton button;
        ITranslatorManager translatorManager;
        ITranslator translator;

        public void Load(IServiceProvider serviceProvider)
        {
            this.windowManager = (IWindowManager)serviceProvider.GetService(typeof(IWindowManager));
            //this.windowManager.ShowMessage("Loading Perst!");
            this.assemblyManager = (IAssemblyManager)serviceProvider.GetService(typeof(IAssemblyManager));
            this.commandBarManager = (ICommandBarManager)serviceProvider.GetService(typeof(ICommandBarManager));
            this.translatorManager = (ITranslatorManager)serviceProvider.GetService(typeof(ITranslatorManager));
            this.translator = this.translatorManager.CreateDisassembler(null, null);
            this.separator = this.commandBarManager.CommandBars["Tools"].Items.AddSeparator();
            this.button = this.commandBarManager.CommandBars["Tools"].Items.AddButton("&Detect Modify Bug", new EventHandler(this.PerstListButton_Click));
        }


        public void Unload()
        {
            //this.windowManager.ShowMessage("Unloading Perst!");
            this.commandBarManager.CommandBars["Tools"].Items.Remove(this.button);
            this.commandBarManager.CommandBars["Tools"].Items.Remove(this.separator);
        }



        private void PerstListButton_Click(object sender, EventArgs e)
        {
            IAssemblyCollection assemblyCollection = this.assemblyManager.Assemblies;
            List<IAssembly> assemblies = new List<IAssembly>(assemblyCollection.Count);
            foreach (IAssembly assembly in assemblyCollection)
            {
                assemblies.Add(assembly);
            }
            messages = new StringBuilder();
            ITranslator translator = this.translatorManager.CreateDisassembler(null, null);
            foreach (IAssembly assembly in assemblies)
            {
                if (!assembly.Location.StartsWith("%SystemRoot%")
                    && !assembly.Location.StartsWith("%ProgramFiles%"))
                {
                    foreach (IModule module in assembly.Modules)
                    {
                        foreach (ITypeDeclaration type in module.Types)
                        {
                            if (!type.Namespace.StartsWith("Perst"))
                            {
                                processType(type);
                            }
                        }
                    }
                }
            }
            message(messages.Length != 0 ? messages.ToString() : "No bugs detected");
        }

        bool IsPersistent(ITypeReference type)
        {
            while (type != null)
            {
                ITypeDeclaration decl = type.Resolve();
                if (decl != null)
                {
                    if (decl.Namespace == "Perst" && decl.Name == "Persistent")
                    {
                        return true;
                    }
                    type = decl.BaseType;
                }
                else
                {
                    break;
                }
            }
            return false;
        }

        void processType(ITypeDeclaration type)
        {
            foreach (IMethodDeclaration mth in type.Methods)
            {
                processMethod(mth);
            }
            /*
            foreach (IPropertyDeclaration prop in type.Properties)
            {
                processMethod(prop.GetMethod);
                processMethod(prop.SetMethod);
            }
            */
            foreach (ITypeDeclaration nestedType in type.NestedTypes)
            {
                processType(nestedType);
            }
        }

        void join(Dictionary<IExpression, List<IExpression>> dst, Dictionary<IExpression, List<IExpression>> src)
        {
            foreach (KeyValuePair<IExpression, List<IExpression>> pair in src)
            {
                if (!dst.ContainsKey(pair.Key))
                {
                    dst.Add(pair.Key, pair.Value);
                }
            }
        }

        void processStatement(IStatement stmt)
        {
            Dictionary<IExpression, List<IExpression>> beforeMutators;
            if (stmt is IBlockStatement)
            {
                foreach (IStatement s in ((IBlockStatement)stmt).Statements)
                {
                    processStatement(s);
                }
            }
            else if (stmt is IWhileStatement)
            {
                beforeMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                processExpression(((IWhileStatement)stmt).Condition);
                processStatement(((IWhileStatement)stmt).Body);
                join(mutators, beforeMutators);
            }
            else if (stmt is IForEachStatement)
            {
                beforeMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                processExpression(((IForEachStatement)stmt).Expression);
                processStatement(((IForEachStatement)stmt).Body);
                join(mutators, beforeMutators);
            }
            else if (stmt is IForStatement)
            {
                beforeMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                processExpression(((IForStatement)stmt).Condition);
                processStatement(((IForStatement)stmt).Increment);
                processStatement(((IForStatement)stmt).Initializer);
                processStatement(((IForStatement)stmt).Body);
                join(mutators, beforeMutators);
            }
            else if (stmt is ITryCatchFinallyStatement)
            {
                processStatement(((ITryCatchFinallyStatement)stmt).Try);
                processStatement(((ITryCatchFinallyStatement)stmt).Fault);
                processStatement(((ITryCatchFinallyStatement)stmt).Finally);
                foreach (ICatchClause clause in ((ITryCatchFinallyStatement)stmt).CatchClauses)
                {
                    processExpression(clause.Condition);
                    processStatement(clause.Body);
                }
            }
            else if (stmt is IConditionStatement)
            {
                beforeMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                processExpression(((IConditionStatement)stmt).Condition);
                processStatement(((IConditionStatement)stmt).Then);
                Dictionary<IExpression, List<IExpression>> afterMutators = mutators;
                mutators = beforeMutators;
                processStatement(((IConditionStatement)stmt).Else);
                join(mutators, afterMutators);
            }
            else if (stmt is ISwitchStatement)
            {
                beforeMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                Dictionary<IExpression, List<IExpression>> afterMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                processExpression(((ISwitchStatement)stmt).Expression);
                Dictionary<IExpression, List<IExpression>> enterMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                foreach (ISwitchCase cases in ((ISwitchStatement)stmt).Cases)
                {
                    mutators = new Dictionary<IExpression, List<IExpression>>(beforeMutators);
                    processStatement(cases.Body);
                    join(afterMutators, mutators);
                }
                mutators = afterMutators;
            }
            else if (stmt is IUsingStatement)
            {
                processExpression(((IUsingStatement)stmt).Expression);
                processStatement(((IUsingStatement)stmt).Body);
            }
            else if (stmt is ILockStatement)
            {
                IExpression expr = ((ILockStatement)stmt).Expression;
                processExpression(expr);
                processStatement(((ILockStatement)stmt).Body);
            }
            else if (stmt is ILabeledStatement)
            {
                processStatement(((ILabeledStatement)stmt).Statement);
            }
            else if (stmt is IDoStatement)
            {
                beforeMutators = new Dictionary<IExpression, List<IExpression>>(mutators);
                processStatement(((IDoStatement)stmt).Body);
                processExpression(((IDoStatement)stmt).Condition);
                join(mutators, beforeMutators);
            }
            else if (stmt is IExpressionStatement)
            {
                processExpression(((IExpressionStatement)stmt).Expression);
            }
            else if (stmt is IBreakStatement || stmt is IGotoStatement || stmt is IContinueStatement || stmt is IMethodReturnStatement)
            {
                reportErrors();
            }
        }

        void reportErrors()
        {
            foreach (KeyValuePair<IExpression, List<IExpression>> pair in mutators)
            {
                IExpression expr = pair.Key;
                if (!reportedErrors.ContainsKey(expr))
                {
                    reportedErrors[expr] = true;
                }
            }
            mutators.Clear();
        }

        bool IsNonSerialized(IFieldDeclaration field)
        {
            foreach (ICustomAttribute attr in field.Attributes)
            {
                if (((ITypeReference)attr.Constructor.DeclaringType).Name.Equals("NonSerializedAttribute"))
                {
                    return true;
                }
            }
            return false;
        }

        void processExpression(IExpression expr)
        {
            processExpression(expr, false);
        }

        void processExpression(IExpression expr, bool lvalue)
        {
            if (expr is IMethodInvokeExpression)
            {
                IMethodInvokeExpression call = (IMethodInvokeExpression)expr;
                IExpression methodExpr = call.Method;
                if (methodExpr is IMethodReferenceExpression)
                {
                    IMethodReference method = ((IMethodReferenceExpression)methodExpr).Method;
                    if (method.Name == "Modify" || method.Name == "Store")
                    {
                        IExpression target = ((IMethodReferenceExpression)methodExpr).Target;
                        if (target is IBaseReferenceExpression && thisExpr != null)
                        {
                            target = thisExpr;
                        }
                        mutators.Remove(target);
                    }
                }
                processExpression(methodExpr);
                foreach (IExpression arg in call.Arguments)
                {
                    processExpression(arg);
                }
            }
            else if (expr is IAssignExpression)
            {
                processExpression(((IAssignExpression)expr).Target, true);
                processExpression(((IAssignExpression)expr).Expression);
            }
            else if (expr is IBinaryExpression)
            {
                processExpression(((IBinaryExpression)expr).Left);
                processExpression(((IBinaryExpression)expr).Right);
            }
            else if (expr is IUnaryExpression)
            {
                processExpression(((IUnaryExpression)expr).Expression);
            }
            else if (expr is ICastExpression)
            {
                processExpression(((ICastExpression)expr).Expression);
            }
            else if (expr is IArrayIndexerExpression)
            {
                processExpression(((IArrayIndexerExpression)expr).Target, lvalue);
                foreach (IExpression index in ((IArrayIndexerExpression)expr).Indices)
                {
                    processExpression(index);
                }
            }
            else if (expr is IArrayCreateExpression)
            {
                processExpression(((IArrayCreateExpression)expr).Initializer);
                foreach (IExpression dim in ((IArrayCreateExpression)expr).Dimensions)
                {
                    processExpression(dim);
                }
            }
            else if (expr is ICanCastExpression)
            {
                processExpression(((ICanCastExpression)expr).Expression);
            }
            else if (expr is IConditionExpression)
            {
                processExpression(((IConditionExpression)expr).Condition);
                processExpression(((IConditionExpression)expr).Then);
                processExpression(((IConditionExpression)expr).Else);
            }
            else if (expr is IDelegateInvokeExpression)
            {
                processExpression(((IDelegateInvokeExpression)expr).Target);
                foreach (IExpression arg in ((IDelegateInvokeExpression)expr).Arguments)
                {
                    processExpression(arg);
                }
            }
            else if (expr is IEventReferenceExpression)
            {
                processExpression(((IEventReferenceExpression)expr).Target);
            }
            else if (expr is IFieldReferenceExpression)
            {
                IExpression target = ((IFieldReferenceExpression)expr).Target;
                if (lvalue)
                {
                    IFieldReference fieldRef = ((IFieldReferenceExpression)expr).Field;
                    IFieldDeclaration field = fieldRef.Resolve();
                    if (field != null 
                        && IsPersistent(field.DeclaringType as ITypeReference) 
                        && !field.Static
                        && !IsNonSerialized(field))
                    {
                        List<IExpression> updateList;
                        if (target is IBaseReferenceExpression && thisExpr != null)
                        {
                            target = thisExpr;
                        }
                        if (!mutators.TryGetValue(target, out updateList))
                        {
                            mutators[target] = updateList = new List<IExpression>();
                        }
                        updateList.Add(expr);
                    }
                }
                processExpression(target);
            }
            else if (expr is IObjectCreateExpression)
            {
                processExpression(((IObjectCreateExpression)expr).Initializer);
                foreach (IExpression arg in ((IObjectCreateExpression)expr).Arguments)
                {
                    processExpression(arg);
                }
            }
            else if (expr is IBlockExpression)
            {
                foreach (IExpression elem in ((IBlockExpression)expr).Expressions)
                {
                    processExpression(elem);
                }
            }
            else if (expr is IPropertyIndexerExpression)
            {
                processExpression(((IPropertyIndexerExpression)expr).Target);
                foreach (IExpression index in ((IPropertyIndexerExpression)expr).Indices)
                {
                    processExpression(index);
                }
            }
            else if (expr is IPropertyReferenceExpression)
            {
                processExpression(((IPropertyReferenceExpression)expr).Target);
            }
            else if (expr is IThisReferenceExpression)
            {
                thisExpr = expr;
            }
        }

        public static string GetTypeName(ITypeReference type)
        {
            if (type == null)
            {
                return null;
            }
            ITypeReference owner = type.Owner as ITypeReference;
            if (owner != null)
            {
                return GetTypeName(owner) + "." + type.Name;
            }
            else
            {
                string ns = type.Namespace;
                return ns != null && ns.Length != 0 ? ns + "." + type.Name : type.Name;
            }
        }

        public static string GetMethodName(IMethodDeclaration method)
        {
            return GetTypeName(method.DeclaringType as ITypeReference) + "." + method.Name;
        }

        internal void processMethod(IMethodDeclaration method)
        {
            IMethodDeclaration methodDecl = translator.TranslateMethodDeclaration(method);
            if (!(methodDecl is IConstructorDeclaration))
            {
                IBlockStatement body = methodDecl.Body as IBlockStatement;
                if (body != null)
                {
                    mutators = new Dictionary<IExpression, List<IExpression>>();
                    reportedErrors = new Dictionary<IExpression, bool>();
                    processStatement(body);
                    reportErrors();
                    foreach (IExpression expr in reportedErrors.Keys)
                    {

                        String obj = expr is IFieldReferenceExpression 
                            ? ((IFieldReferenceExpression)expr).Field.Name 
                            : expr is IVariableReferenceExpression
                              ? ((IVariableReferenceExpression)expr).Variable.Resolve().Name
                              : expr.ToString();
                        messages.Append("Object \"" + obj + "\" is updated in method " + GetMethodName(methodDecl) + " without marking as modified\n");
                    }
                }
            }
        }

        internal void processMethod(IMethodReference mth)
        {
            if (mth != null)
            {
                IMethodDeclaration decl = mth.Resolve();
                if (decl != null)
                {
                    processMethod(decl);
                }
            }
        }

        public virtual void message(String msg)
        {
            this.windowManager.ShowMessage(msg);
            StreamWriter writer = new StreamWriter("bug.lst");
            writer.Write(msg);
            writer.Close();

        }

        private Dictionary<IExpression, List<IExpression>> mutators;
        private Dictionary<IExpression, bool> reportedErrors;
        private StringBuilder messages;
        private IExpression thisExpr;
    }
}