public void Main(string argument) {

   string debugScreenName = "ShonDebugScreen";
     
   IMyGridTerminalSystem station = GridTerminalSystem;

   IMyTextPanel screenBlock = station.GetBlockWithName(debugScreenName) as IMyTextPanel;
     
   DebugScreen screen = new DebugScreen(ref screenBlock);

   var refineries = GetBlocks<IMyRefinery>(station, "refinery");
   var crushers = GetBlocks<IMyRefinery>(station, "crusher");
   var assemblers = GetBlocks<IMyAssembler>(station);
   var stoneContainers = GetBlocks<IMyCargoContainer>(station, "stone");
   var smallContainers = GetBlocks<IMyCargoContainer>(station, "small");
   var medContainers = GetBlocks<IMyCargoContainer>(station, "medium");

   string transferReport = "";
   transferReport += Transfer("hydro", assemblers, stoneContainers);
   transferReport += Transfer("hydro", smallContainers, stoneContainers);
   transferReport += Transfer("hydro", medContainers, stoneContainers);
   
   transferReport += Transfer("stone", crushers, stoneContainers, 1);
   transferReport += Transfer("stone", refineries, crushers);
   transferReport += Transfer("stone", refineries, stoneContainers);
   transferReport += Transfer("stone", assemblers, stoneContainers);
   transferReport += Transfer("stone", smallContainers, stoneContainers);
   transferReport += Transfer("stone", medContainers, stoneContainers);

   screen.setText("Stone Transfer Report "+DateTime.Now.ToString());
   screen.append("\n-----------------------------");
   screen.append(transferReport);
   screen.append("end");
}

public List<IMyTerminalBlock> GetBlocks<T>(IMyGridTerminalSystem grid, string filterOn = ""){

   var searchResults = new List<IMyTerminalBlock>(); 
 
    grid.GetBlocksOfType<T>(searchResults);

    var blocks= new List<IMyTerminalBlock>();

    for(var i=0; i< searchResults.Count;i++){ 
         
        var name = searchResults[i].CustomName.ToLower(); 
 
        if(name.Contains(filterOn) || filterOn.Length == 0){ 
            blocks.Add(searchResults[i]);
        } 
    }

    return blocks;
}

public string Transfer(
                        string keyWord, 
                        List<IMyTerminalBlock> source, 
                        List<IMyTerminalBlock> dest, 
                        int inventoryIndex = 0){

    string report = String.Empty;

    for(var i =0; i< source.Count; i++){

        var inventory = ((IMyInventoryOwner)source[i]).GetInventory(inventoryIndex);

        var items = inventory.GetItems();

        for(var k=0; k<items.Count; k++){
            var slotItem = items[k].Content.SubtypeName.ToLower();

            if(slotItem.Contains(keyWord)){
                for(var g=0; g< dest.Count; g++){
                    var destInventory = ((IMyInventoryOwner)dest[g]).GetInventory(0);
                    
                    var currentVolume = (int)Math.Round((float)destInventory.CurrentVolume);
                    var maxVolume = (int)Math.Round((float)destInventory.MaxVolume);

                    if(currentVolume != maxVolume){
                        inventory.TransferItemTo(destInventory, k, null, true, 100000);
                        report += String.Format("{0}'s {1} -> {2} {3}/{4}\n", 
                            source[i].CustomName, 
                            slotItem, 
                            dest[g].CustomName, 
                            currentVolume, 
                            maxVolume); 
                    }

                }       

            }
        }
        
    }

    return report;
}

public void Fail(Exception err, DebugScreen console){
    console.setText(err.ToString());
}


public class DebugScreen {

    IMyTextPanel screen;

    public DebugScreen(ref IMyTextPanel theScreen){
        this.screen = theScreen;
    }

    public void refreshScreen(){
        this.screen.ApplyAction("OnOff_Off");
        this.screen.ApplyAction("OnOff_On");    
    }

    public void setText(string text){
        this.screen.WritePublicText(text);
        refreshScreen();
    }

    public void append(string text){ 
        this.screen.WritePublicText(text+"\n", true); 
        refreshScreen(); 
    }    
}