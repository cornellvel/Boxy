
public class EnvVariables {

	// Empty static class for globals
	public static int MovementTrackerInterval = 3;
	public static int CubeColorInterval = 3;
	public static int CubeSizeInterval = 1;

    public static string ServerIP = "localhost";

	public static string BaseURI = "http://" + ServerIP + ":8000/";
	public static string DisplayName = "Avatar X";
	public static string Comparator = "Avatar Y";

    public static string AvatarType = "0818avatar2";

	public static bool debug = false;
    public static bool timerEnabled = false;
	public static float duration = 5f * 60f;

}
