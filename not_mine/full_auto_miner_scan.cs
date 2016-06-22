//Scan

int max_GFD = 3; // 3 is the minimum required for now. You can set it higher, but it may tax the server.     
int max_targets = 50; // this does not do anything for now. Be aware that having a high number of grids will lag      
int asteroid_check = 180000; // check an asteroid every 15 minutes. Usually it will be hit by a random beam sooner.     
float max_dist = 25000; // this is a very long range, but it works. If you are afraid this causes lag, set it lower.     
double max_v = 120; //  this is used on a hit, to guess wether a target is the same target you were tracking.     
                                     // I set it to 120, because it seems possible to go over the 100 m/s limit.      
double max_acc = 10; // This number is the maximum acceleration a ship is expected to have. Any higher than this     
                                     //and the lock may be lost when making a quick velocity change in the transverse direction.      
int max_loop = 100;     
float max_beam_width = 500;     
  
Vector3D cone1= new Vector3D();  
Vector3D  cone2= new Vector3D();  
bool Stop;  
string ShipName = "Octopus1";  
  
  
  
int Clock=1;  
int TickCount;  
string SearchStr = "                              ------------------------------                              ";  
  
void Main(string argument)       
{     
	var gts = GridTerminalSystem;     
    IMyTimerBlock timer = (IMyTimerBlock)gts.GetBlockWithName(ShipName+ "TimerScan");    
    IMyRemoteControl rc = (IMyRemoteControl)gts.GetBlockWithName(ShipName+ "RemCon");    	  
	if (argument == "Stop")  
		{  
			Stop=!Stop;  
		}  
	else if (argument == "reset")   
		{  
			Storage = "";  
			Stop=false;			  
		}      
  
TickCount++;  
if (TickCount % Clock !=0 )  
{  
	timer.GetActionWithName("TriggerNow").Apply(timer);  
	return;  
}  
   
    //IMyTextPanel grid_panel = (IMyTextPanel)gts.GetBlockWithName(ShipName+ "TP_Rock");      
    Vector3D rc_pos = rc.GetPosition();      
	  
    double radius;       
    Vector3D center = rc_pos;      
    Matrix or, world_or;     
    // we use the orientation vectors below to print the target arrows on the target LCD     
    rc.Orientation.GetMatrix(out or);      
    world_or = rc.WorldMatrix.GetOrientation();      
       
    	Vector3D c_fwd  = Vector3D.Transform(or.Forward, MatrixD.Transpose(or));       
    	Vector3D c_up  = Vector3D.Transform(or.Up, MatrixD.Transpose(or));       
    	Vector3D c_right  = Vector3D.Transform(or.Right, MatrixD.Transpose(or));      
      
    Vector3D fwd_wrld = Vector3D.Transform(c_fwd, world_or);      
    	Vector3D up_wrld  = Vector3D.Transform(c_up, world_or);      
    	Vector3D right_wrld  = Vector3D.Transform(c_right, world_or);     
     
    int GFD_tick = 0;      
    string[] target_strings = LoadStorage();      
      
    List<Target> targets = new List<Target>();      
    char[] delim = {';'};      
     
    for (int i = 0; i < target_strings.Length; i++) {      
        string[] tar = target_strings[i].Split(delim);      
        targets.Add(new Target{time = int.Parse(tar[0])+1, countdown = int.Parse(tar[1])-1,      
        radius = double.Parse(tar[2]), velocity = GetVector3D(tar[3]), coords = GetVector3D(tar[4]),      
        stat = bool.Parse(tar[5]), grid = bool.Parse(tar[6])});         
    }      
        
    for (int i = targets.Count-1; i > -1; i--) {       
        if (targets[i].countdown < 1 && GFD_tick < max_GFD) { //we check if one of the tracked targets needs updating     
            Vector3D target = targets[i].coords + targets[i].velocity * targets[i].time - rc_pos;      
            // we use the velocity to predict where the target has travelled across the sky.     
            target.Normalize();       
            center = Lock3GFD(rc,rc_pos+target, max_dist, 0f, out radius);        
            if (!center.IsValid()) { // if something went wrong and we get a bad result,      
                                             // we count it as 3 GFD calls and remove the target from our list.      
                GFD_tick += 3;        
                targets.RemoveAt(i);      
            }     
            else {      
                if (center == rc_pos) GFD_tick += 1; //the Lock3GFD method returns after 1 GFD call if it misses     
                else {     
                    GFD_tick += 3;  //if Lock3GF hits, we count it a 3 GFD calls     
                    update_targetlist (targets,  rc_pos, center, radius);  // we only update when something is hit.     
                }     
                if (Math.Abs(radius- targets[i].radius) > 0.1 || (center-rc_pos).Length() < 1) targets.RemoveAt(i);     
                // when the target is too close to be right or its radius is not correct, we remove the target.     
            }     
        }        
    }      
     
    Random rnd = new Random();      
     
    //The while loop below shoots GFD beams into random directions to find new targets.      
    //It uses spherical coördinates.      
	int loop_count = 0;  
     
    while (GFD_tick < max_GFD-2 && loop_count < max_loop) { // we loop over the allowed number of GFD     
        loop_count++;                             // we take a maximum of 100 loops to prevent problems with the script     
        double theta = rnd.NextDouble() * 2 * Math.PI;  // theta is between 0 and 2 pi     
		double z=rnd.NextDouble();  		  
        double z_fac = Math.Sqrt(1- Sq(z)); //the squareroot of 1 minus z-square         
        double x = z_fac * Math.Cos(theta);     
        double y = z_fac * Math.Sin(theta);     
     
        Vector3D rel_target = new Vector3D(x,-z,y); //spherical coordinates transformed to cartesian.     
		rel_target = Vector3D.Transform(rel_target, rc.WorldMatrix)-rc.GetPosition();  
    
        float beam_width = max_beam_width;     
        for(int i = 0; i < targets.Count; i++) { //this checks how wide the beam can be without crossing known targets     
            Vector3D target_dir = targets[i].coords - rc_pos;     
            float target_beam_width = (float)(rel_target.Cross(target_dir)).Length();     
            target_beam_width -=(float)targets[i].radius - 1;     
            if (target_beam_width < beam_width) beam_width = target_beam_width;     
            if (target_beam_width < 0) break;     
        }     
     
        if (beam_width > 0) { // if the beam_width is negative it will hit a known target even with beam_width 0;     
            Vector3D target = rc_pos + rel_target; // we don't need to normalize because the length of rel_target = 1.      
            center = Lock3GFD(rc, target, max_dist, beam_width, out radius);        
     
            if (center == rc_pos) GFD_tick += 1;  //the Lock3GFD method returns after 1 GFD call if it misses     
            else {      
                GFD_tick += 3; //if Lock3GF hits, we count it a 3 GFD calls     
                update_targetlist (targets,  rc_pos, center, radius); // we only update when something is hit.     
            }     
        }     
    }      
	string strRoids="";  
	int ii=0;  
    for(int i = 0; i < targets.Count; i++) {       
        Vector3D coords = targets[i].coords;   
        if ((targets[i].stat) && (Math.Round(targets[i].radius/Math.Sqrt(3),1)==8))   
		{  
            strRoids += ii.ToString() +";";  
            strRoids += Math.Round(coords.GetDim(0),1) +";"+ Math.Round(coords.GetDim(1),1) +";"+ Math.Round(coords.GetDim(2),1) +";";  
            strRoids += Math.Round((targets[i].coords - rc_pos).Length(),0).ToString() +";\n";  
			ii++;  
		}  
    }  
	int tt= (int)((TickCount / Clock)%60);  
	string SearchSubStr = SearchStr.Substring(tt, 30);  
	strRoids="\nRocks found:"+(ii).ToString()+";\n"+SearchSubStr+"\n"+strRoids;	  
	 
	IMyTextPanel grid_panel = (IMyTextPanel)gts.GetBlockWithName(ShipName+ "TP_Rock"); 
	grid_panel.ShowTextureOnScreen(); 
	grid_panel.WritePublicText(strRoids);  
    grid_panel.ShowPublicTextOnScreen();    
    grid_panel.GetActionWithName("OnOff_On").Apply(grid_panel);  
				 
    double dotprod = -1;     
    int target_index = -1;     
     
    Storage = "";      
    StringBuilder builder = new StringBuilder();       
    for(int j = 0; j < targets.Count; j++) {      
        builder.Append(targets[j].ToString());      
        if (j < targets.Count - 1) builder.Append("\n");      
    }      
    Storage = builder.ToString();      
	if (!Stop)  
		timer.GetActionWithName("TriggerNow").Apply(timer);      
}      
      
Vector3D GetVector3D(string rString){  // convert String to Vector3D                       
    if (rString == "0") rString = "(X:0 Y:0 Z:0)";                       
    string[] temp = rString.Substring(1,rString.Length-2).Split(' ');                        
    double x = double.Parse(temp[0].Substring(2,temp[0].Length-2));                       
    double y = double.Parse(temp[1].Substring(2,temp[1].Length-2));                        
    double z = double.Parse(temp[2].Substring(2,temp[2].Length-2));                          
    Vector3D rValue = new Vector3D(x,y,z);              
    return rValue;                      
  }                                                             
      
Vector3D Lock3GFD (IMyRemoteControl rc, Vector3D target, float max_dist, float beam_width, out double radius) {       
    // this method is commented on the Keen forums except the radius part at the bottom.      
    // I did not come up with this myself, exept the radius calculation. That part is mine.     
    Vector3D R = rc.GetPosition();      
    Vector3D F1 = rc.GetFreeDestination(target, max_dist, beam_width);  // F,O and R refer to PennyWise's pictures     
    Vector3D RF1 = F1-R;     
    if (RF1.Length() < 1) {     
        radius = 0;     
        return R;     
    }     
    Vector3D RO_norm = Vector3D.Normalize(target-R);      
    Vector3D O1 = R + (F1-R).Length()*RO_norm;        
    Vector3D dO = Vector3D.Normalize(F1-O1);  //named differently in Pennywise's pictures      
    Vector3D O2 = O1 - dO;  // shift the points to the inside of the sphere instead of the outside to avoid miss          
    Vector3D O3 = O2 - dO;        
    Vector3D F2 = rc.GetFreeDestination(O2, max_dist, beam_width);        
    Vector3D F3 = rc.GetFreeDestination(O3, max_dist, beam_width);        
    Vector3D circle_norm = Vector3D.Cross(F1-O1,O1-R);        
    Vector3D F12 = (F1+F2)/2;        
    Vector3D F23 = (F2+F3)/2;        
      
    Vector3D FC12 = circle_norm.Cross(F2-F1);        
    Vector3D FC23 = circle_norm.Cross(F3-F2);        
    Vector3D T = Intersect(F12,FC12, F23,FC23);       
    Vector3D RT = T-R;         
    Vector3D RT_RF1 = RT + RF1;       
      
    // getting line parameter "k" to find O along the line through R and O. We use that FO and TO are perpendicular.      
          
    double k = (RT_RF1.Dot(RO_norm) - Math.Sqrt(Sq(RT_RF1.Dot(RO_norm))-4*RT.Dot(RF1)))/2;      
    radius = (T- R - k * RO_norm).Length() - beam_width;      
    if (double.IsNaN(radius)) T = R;     
       
    return T;      
}       
       
Vector3D Intersect(Vector3D Pos1, Vector3D Vec1, Vector3D Pos2, Vector3D Vec2)         
{         
  // this method intersects two vectors that start at pos 1 and pos 2 and point in the direction of vec1 and vec2      
    Vec1.Normalize();       
    Vec2.Normalize();       
    double D11, D22, D12, DP1, DP2, C12, S1, S2;        
        
    Vector3D Pos_change = Pos2 - Pos1;         
    Vector3D Cross12 = Vec1.Cross(Vec2);         
        
    D11 = Vec1.LengthSquared();         
    D22= Vec2.LengthSquared();         
    D12 = Vec1.Dot(Vec2);         
    DP1= Pos_change.Dot(Vec1);          
    DP2= Pos_change.Dot(Vec2);        
    C12 = Cross12.LengthSquared();        
        
    S1= (D22*DP1 - DP2*D12) / C12;           
    S2= (D12*DP1 - DP2*D11) / C12;         
        
    Vector3D P1 = Pos1 + S1 * Vec1;        
    Vector3D P2 = Pos2 + S2 *Vec2;        
         
    return (P1+P2)/2;         
}      
      
double Sq(double number) {              // just for squaring stuff.       
    	double squared = number*number;       
    	return squared;       
}      
      
string[] LoadStorage()                           
{                                                    
    char[] delim = {'\n'};            
    string[] data= Storage.Split(delim);                                  
    if (Storage == "") return new string[0];                                           
    else return data;                           
}                           
                            
public class Target { // this class describes a datapoint (target) with all the information that goes with it.     
    public int time;    // time since last scan (ticks)     
    public int countdown; // countdown to next scan     
    public double radius; // radius of the target, used for identification     
    public Vector3D velocity; // velocity of the target      
    public Vector3D coords;  // the GPS coords of the target     
    public bool stat; // this is true when a target has velocity 0 and is of a radius that an asteroid may have     
    public bool grid; // this is true when a "supposed" asteroid has ever gotten a velocity.      
      
    public override string ToString()       
    {       
       return time + ";" + countdown + ";" + radius + ";" + velocity + ";"+ coords + ";" + stat + ";" + grid;      
    }      
}       
      
List<Target> update_targetlist (List<Target> targets,  Vector3D rc_pos, Vector3D center, double radius)       
{       
    if (center.IsValid() && (center - rc_pos).Length() > 1) {         
        bool new_target = true;    // if this is true we have a new target, it may be made false below.     
        for (int i = 0; i<targets.Count; i++) {          
            Vector3D offset = center - targets[i].coords; // we find out if the target is close to another known target     
     
            // the complicated "if" below checks two situations. If the target is a grid, it checks if a target with the     
            // same radius exists at a location within a sphere of radius "possible distance travelled since last check"     
     
            // if the target is an asteroid it simply checks if the radius is correct and it has the same location     
     
             if ((!targets[i].stat && Math.Abs(radius - targets[i].radius) < 0.1 &&       
                offset.Length() < Math.Max(targets[i].time,1) * max_v/60*Clock) || (targets[i].stat &&      
                Math.Abs(radius - targets[i].radius) < 0.2 && offset.Length() < 1)) {        
     
                    new_target = false;         
                    if (targets[i].time > 0) {       
                        targets[i].radius = radius;         
                        targets[i].velocity = (center-targets[i].coords)/targets[i].time;       
     
                        if ((targets[i].velocity).Length() > 1) { // if a target has ever moved, it is not considered an asteroid     
                            targets[i].grid = true;       
                            targets[i].stat = false;     
                        }     
     
                        if (targets[i].stat) targets[i].countdown = asteroid_check;      
                        else targets[i].countdown = (int)Math.Floor(Math.Sqrt(radius/max_acc)*60/Clock);      
     
                        targets[i].time = 0;         
                        targets[i].coords = center;         
                        double real_radius = radius/Math.Sqrt(3);         
                             
                        bool asteroid = false;         
                        if (targets[i].grid == false) {          
                            for (int j = 0; j < 7; j++) {         
                            if (Math.Abs(real_radius - 8* Math.Pow(2,j)) < 0.1 && (targets[i].velocity).Length() < 1) {     
                                targets[i].stat = true;     
                            // this checks whether the radius is the same as a possible asteroids radius and if it moves.     
                            // if so, then it is considered a stationary target (possibly asteroid)     
                            // if it ever does move, this check will never be made again. Asteroids never move.     
                            }     
                                 
                        }            
                    }            
                }         
            }         
        }      
     
        if (new_target) {         
            targets.Add(new Target{time = 0, countdown = 1, radius = radius,  velocity = new Vector3D(0,0,0), coords = center,         
            stat = false, grid = false});            
        }         
    }       
    return targets;       
}