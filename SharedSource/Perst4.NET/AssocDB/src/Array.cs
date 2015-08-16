namespace Perst.Assoc
{
using System;

///
/// <summary>
///  Helper class for manipulations with arrays
/// </summary>
///
public class Arrays
{
    public static int[] Remove(int[] arr, int i) 
    { 
        return Remove(arr, i, 1);
    }

    public static int[] Remove(int[] arr, int i, int count) 
    { 
        int n = arr.Length;
        int[] newArr = new int[n-count];
        Array.Copy(arr, 0, newArr, 0, i);
        Array.Copy(arr, i+count, newArr, i, n-i-count);
        return newArr;
    }

    public static String[] Remove(String[] arr, int i) 
    { 
        return Remove(arr, i, 1);
    }

    public static String[] Remove(String[] arr, int i, int count) 
    { 
        int n = arr.Length;
        String[] newArr = new String[n-count];
        Array.Copy(arr, 0, newArr, 0, i);
        Array.Copy(arr, i+count, newArr, i, n-i-count);
        return newArr;
    }

    public static double[] Remove(double[] arr, int i) 
    { 
        return Remove(arr, i, 1);
    }

    public static double[] Remove(double[] arr, int i, int count) 
    { 
        int n = arr.Length;
        double[] newArr = new double[n-count];
        Array.Copy(arr, 0, newArr, 0, i);
        Array.Copy(arr, i+count, newArr, i, n-i-count);
        return newArr;
    }

    public static String[] Insert(String[] arr, int i, String value) 
    { 
        int n = arr.Length;
        String[] newArr = new String[n+1];
        Array.Copy(arr, 0, newArr, 0, i);
        newArr[i] = value;
        Array.Copy(arr, i, newArr, i+1, n-i);
        return newArr;
    }

    public static double[] Insert(double[] arr, int i, double value) 
    { 
        int n = arr.Length;
        double[] newArr = new double[n+1];
        Array.Copy(arr, 0, newArr, 0, i);
        newArr[i] = value;
        Array.Copy(arr, i, newArr, i+1, n-i);
        return newArr;
    }

    public static int[] Insert(int[] arr, int i, int value) 
    { 
        return Insert(arr, i, value, 1);
    }

    public static int[] Insert(int[] arr, int i, int value, int count) 
    { 
        int n = arr.Length;
        int[] newArr = new int[n+count];
        Array.Copy(arr, 0, newArr, 0, i);
        for (int j = 0; j < count; j++) { 
            newArr[i+j] = value;
        }
        Array.Copy(arr, i, newArr, i+count, n-i);
        return newArr;
    }

    public static String[] Insert(String[] arr, int i, String[] values) 
    { 
        int n = arr.Length;
        String[] newArr = new String[n+values.Length];
        Array.Copy(arr, 0, newArr, 0, i);
        Array.Copy(values, 0, newArr, i, values.Length);
        Array.Copy(arr, i, newArr, i+values.Length, n-i);
        return newArr;
    }

    public static double[] Insert(double[] arr, int i, double[] values) 
    { 
        int n = arr.Length;
        double[] newArr = new double[n+values.Length];
        Array.Copy(arr, 0, newArr, 0, i);
        Array.Copy(values, 0, newArr, i, values.Length);
        Array.Copy(arr, i, newArr, i+values.Length, n-i);
        return newArr;
    }

    public static int[] Truncate(int[] arr, int size) 
    { 
        int[] newArr = new int[size];
        Array.Copy(arr, 0, newArr, 0, size);
        return newArr;
    }
}
}
