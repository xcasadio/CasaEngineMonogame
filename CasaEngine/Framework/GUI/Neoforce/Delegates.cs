namespace TomShane.Neoforce.Controls;

public delegate void DeviceEventHandler(DeviceEventArgs e);
public delegate void SkinEventHandler(EventArgs e);

public delegate void EventHandler(object sender, EventArgs e);
public delegate void MouseEventHandler(object sender, MouseEventArgs e);
public delegate void KeyEventHandler(object sender, KeyEventArgs e);
public delegate void GamePadEventHandler(object sender, GamePadEventArgs e);
public delegate void DrawEventHandler(object sender, DrawEventArgs e);
public delegate void MoveEventHandler(object sender, MoveEventArgs e);
public delegate void ResizeEventHandler(object sender, ResizeEventArgs e);
public delegate void WindowClosingEventHandler(object sender, WindowClosingEventArgs e);
public delegate void WindowClosedEventHandler(object sender, WindowClosedEventArgs e);
public delegate void ConsoleMessageEventHandler(object sender, ConsoleMessageEventArgs e);