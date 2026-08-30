using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// Excel COM の遅延バインディング。Type.InvokeMember のみ。STA 以外では呼べない。
    /// culture は LCID 1033（en-US）。Thread.CurrentCulture は変えない。
    /// </summary>
    public static class ComInvoker
    {
        private static readonly CultureInfo InvokeCulture = CultureInfo.GetCultureInfo(1033);

        /// <summary>
        /// プロパティを読む。IDispatch 種別差は内部で吸収する。
        /// </summary>
        /// <param name="target">COM オブジェクト。</param>
        /// <param name="name">メンバー名。</param>
        /// <param name="args">インデックス引数。無ければ空でよい。</param>
        /// <returns>値。</returns>
        public static object GetProperty(object target, string name, params object[] args)
        {
            return Invoke(target, name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty, args);
        }

        /// <summary>
        /// プロパティを書く。
        /// </summary>
        /// <param name="target">COM オブジェクト。</param>
        /// <param name="name">メンバー名。</param>
        /// <param name="value">値。</param>
        public static void SetProperty(object target, string name, object value)
        {
            Invoke(target, name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty, new object[] { value });
        }

        /// <summary>
        /// メソッドを呼ぶ。IDispatch 種別差は内部で吸収する。
        /// </summary>
        /// <param name="target">COM オブジェクト。</param>
        /// <param name="name">メソッド名。</param>
        /// <param name="args">引数。</param>
        /// <returns>戻り値。void なら null。</returns>
        public static object Call(object target, string name, params object[] args)
        {
            return Invoke(target, name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, args);
        }

        /// <summary>
        /// メソッドを ByRef 引数付きで呼ぶ。GetSelection 用。6 引数 InvokeMember は維持する。
        /// </summary>
        /// <param name="target">COM オブジェクト。</param>
        /// <param name="name">メソッド名。</param>
        /// <param name="args">ByRef で更新する引数配列。</param>
        /// <returns>戻り値。void なら null。</returns>
        public static object CallByRef(object target, string name, object[] args)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                throw new InvalidOperationException("Excel COM は STA の UI スレッドでのみ呼べます。");
            }

            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name");
            }

            if (args == null)
            {
                args = new object[0];
            }

            ParameterModifier[] modifiers = null;
            if (args.Length > 0)
            {
                ParameterModifier mod = new ParameterModifier(args.Length);
                int i = 0;
                while (i < args.Length)
                {
                    mod[i] = true;
                    i++;
                }

                modifiers = new ParameterModifier[] { mod };
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod;
            try
            {
                return target.GetType().InvokeMember(name, flags, null, target, args, modifiers, InvokeCulture, null);
            }
            catch (Exception ex)
            {
                throw UnwrapTargetInvocation(ex);
            }
        }

        private static object Invoke(object target, string name, BindingFlags flags, object[] args)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                throw new InvalidOperationException("Excel COM は STA の UI スレッドでのみ呼べます。");
            }

            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name");
            }

            if (args == null)
            {
                args = new object[0];
            }

            try
            {
                return InvokeOnce(target, name, flags, args);
            }
            catch (Exception ex)
            {
                Exception inner = UnwrapTargetInvocation(ex);
                BindingFlags alternate;
                if (!IsMemberNotFound(inner) || !TryAlternateInvokeFlags(flags, out alternate))
                {
                    throw inner;
                }

                try
                {
                    return InvokeOnce(target, name, alternate, args);
                }
                catch (Exception retryEx)
                {
                    throw UnwrapTargetInvocation(retryEx);
                }
            }
        }

        // GetProperty と InvokeMethod を 1 回の flags に OR しない（DefaultBinder の曖昧さを避ける）。
        private static object InvokeOnce(object target, string name, BindingFlags flags, object[] args)
        {
            return target.GetType().InvokeMember(name, flags, null, target, args, InvokeCulture);
        }

        private static Exception UnwrapTargetInvocation(Exception ex)
        {
            TargetInvocationException tie = ex as TargetInvocationException;
            if (tie != null && tie.InnerException != null)
            {
                return tie.InnerException;
            }

            return ex;
        }

        private static bool IsMemberNotFound(Exception ex)
        {
            COMException com = ex as COMException;
            if (com == null)
            {
                return false;
            }

            int code = com.ErrorCode;
            if (code == unchecked((int)0x80020003))
            {
                return true;
            }

            if (code == unchecked((int)0x80020006))
            {
                return true;
            }

            return false;
        }

        private static bool TryAlternateInvokeFlags(BindingFlags flags, out BindingFlags alternate)
        {
            BindingFlags getProperty = BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty;
            BindingFlags invokeMethod = BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod;
            if (flags == getProperty)
            {
                alternate = invokeMethod;
                return true;
            }

            if (flags == invokeMethod)
            {
                alternate = getProperty;
                return true;
            }

            alternate = flags;
            return false;
        }
    }
}
