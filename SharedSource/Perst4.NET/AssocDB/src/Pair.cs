namespace Perst.Assoc
{

/// <summary>
/// Name-value pair.
/// Used to get/set items attributes
/// </summary>
public struct Pair 
{ 
    /// <summary>
    /// Attribute name
    /// </summary>
    public string Name  
    {
        get
        {
             return name;
        }
    }

    /// <summary>
    /// Attribute value
    /// </summary>
    public object Value
    {      
        get
        {
             return value;
        }
    }
            
    /// <summary>
    /// Pair constructor
    /// </summary>
    /// <param name="name">attribute name</param>
    /// <param name="value">attribute value</param>
    public Pair(string name, object value) 
    { 
        this.name = name;
        this.value = value;
    }

    string name;
    object value;
}
}