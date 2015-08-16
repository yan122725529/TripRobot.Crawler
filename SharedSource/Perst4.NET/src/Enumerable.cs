namespace Perst
{
    using System;


#if NET_FRAMEWORK_20
    using System.Collections.Generic;
#endif
    using System.Collections;

    /// <summary>
    /// Helper class for working with enumerable returned by Query, Database and GenericIndex search methods 
    /// </summary>
    public class Enumerable
    {
#if NET_FRAMEWORK_20
         /// <summary>
         /// Get first selected object. This methos can be used when single selected object is needed.
         /// Please notive, that this method doesn't check if selection contain more than one object
         /// </summary>
         /// <param name="enumerable">selection</param>
         /// <returns>first selected object or null if selection is empty</returns>
         ///         
         public static T First<T>(IEnumerable<T> enumerable) where T:class
         {
              IEnumerator<T> enumerator = enumerable.GetEnumerator();
              if (enumerator.MoveNext())
              {
                  return enumerator.Current;
              }
              return null;
         }

         /// <summary>
         /// Convert selection to array 
         /// </summary>
         /// <param name="enumerable">selection</param>
         /// <returns>array with the selected objects</returns>
         /// 
         public static T[] ToArray<T>(IEnumerable<T> enumerable) where T:class
         {
              List<T> list = new List<T>();
              foreach (T obj in enumerable)
              {
                  list.Add(obj);
              }
              return list.ToArray();   
         }

         /// <summary>
         /// Get number of selected objects         
         /// </summary>
         /// <param name="enumerable">selection</param>
         /// <returns>selection size</returns>
         /// 
         public static int Count<T>(IEnumerable<T> enumerable) where T:class
         {
             int count = 0;
             foreach (T obj in enumerable)
             {
                 count += 1;
             }
             return count;
         }             
#endif
         /// <summary>
         /// Get first selected object. This methos can be used when single selected object is needed.
         /// Please notive, that this method doesn't check if selection contain more than one object
         /// </summary>
         /// <param name="enumerable">selection</param>
         /// <returns>first selected object or null if selection is empty</returns>
         ///         
         public static object First(IEnumerable enumerable)
         {
              IEnumerator enumerator = enumerable.GetEnumerator();
              if (enumerator.MoveNext())
              {
                  return enumerator.Current;
              }
              return null;
         }

         /// <summary>
         /// Convert selection to array 
         /// </summary>
         /// <param name="enumerable">selection</param>
         /// <returns>array with the selected objects</returns>
         /// 
         public static object[] ToArray(IEnumerable enumerable)
         {
             ArrayList list = new ArrayList();
             foreach (object obj in enumerable)
             {
                 list.Add(obj);
             }
             return list.ToArray();   
         }

         /// <summary>
         /// Get number of selected objects         
         /// </summary>
         /// <param name="enumerable">selection</param>
         /// <returns>selection size</returns>
         /// 
         public static int Count(IEnumerable enumerable)
         {
             int count = 0;
             foreach (object obj in enumerable)
             {
                 count += 1;
             }
             return count;
         }             
     }
}