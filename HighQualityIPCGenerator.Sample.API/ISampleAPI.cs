namespace HQIPC.Sample.API;

/// <summary>
/// A sample plugin API for demonstrating the usage of HQIPC.
/// </summary>
[IpcInterface(ipcNamespace: "HQIPCSample")]
public interface ISampleAPI
{
    /// <summary>
    /// A sample event with a single string parameter and no return value.
    /// </summary>
    event Action<string> SampleEventOneArg;

    /// <summary>
    /// A sample event with no parameters and no return value.
    /// </summary>
    event Action SampleEvent;

    // BAD: Dalamud events cannot have a return type.
    // Uncommenting this will result in the HQIPC01 error message and a build failure.
    //event Func<int, float> SampleEventReturnValue;
    
    /// <summary>
    /// A sample function that accepts two int parameters and returns an int value.
    /// </summary>
    /// <param name="first">The first int parameter.</param>
    /// <param name="second">The second int parameter.</param>
    /// <returns>An int return value.</returns>
    int SampleFunction(int first, int second);

    /// <summary>
    /// A sample function that accpets two string parameters and does not return a value.
    /// </summary>
    /// <param name="first">The first string parameter.</param>
    /// <param name="second">The second string parameter.</param>
    void SampleAction(string first, string second);
}
