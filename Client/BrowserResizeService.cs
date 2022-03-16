using Microsoft.JSInterop;

namespace WordleOff.Client;

public class BrowserResizeService
{
  public static event Func<Task>? OnResize;

  [JSInvokable]
  public static async Task OnBrowserResizeAsync()
  {
    if (OnResize is not null)
      await OnResize.Invoke();
  }  
}