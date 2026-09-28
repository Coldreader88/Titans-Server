using System.Collections.Generic;
using System.Threading;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A character's containers and items, built from the database when the character enters the game.
    ///
    /// The top-level containers use the unique ids the Lobby sent in the player info
    /// (<see cref="PlayerContainers.GetUniqueID"/>); items get unique ids from a server-wide counter.
    /// Worn clothes (garments table) go in the fixed slots of "weared"; rows of the
    /// container table go in their container, and anything in the hangar is treated as a vehicle.
    /// </summary>
    public class PlayerInventory
    {
        /// <summary>
        /// Static ids of the two containers inside a vehicle (Java: Vehicle.write; seen in
        /// UCGOZone-Login.pcap): its armaments and its inventory.
        /// </summary>
        public const int VehicleArmaments = 120002;
        public const int VehicleInventory = 110010;

        /// <summary>
        /// Weared slot holding the vehicle being piloted (Java: Player.pilot "crowns" it there).
        /// </summary>
        public const int PilotSlot = 0;

        /// <summary>
        /// The clothing slots of weared after the vehicle slot, in order. The official capture has the
        /// combat uniform (dress) in slot 1, gloves in slot 6 and the cap (hat) in slot 7; glasses have
        /// no slot.
        /// </summary>
        public static readonly ApparelType[] WearedSlots =
        {
            ApparelType.DRESS, ApparelType.TOP, ApparelType.COAT, ApparelType.BOTTOM,
            ApparelType.SHOES, ApparelType.GLOVES, ApparelType.HAT,
        };

        private static int nextUniqueID = 0x00400000;

        private readonly object sync = new object();
        private readonly Dictionary<uint, ItemNode> nodes = new Dictionary<uint, ItemNode>();

        /// <summary>
        /// Container table rows this class does not load (unknown containers, weared); saved back unchanged.
        /// </summary>
        private readonly List<CharacterItem> kept = new List<CharacterItem>();

        public List<ItemNode> Containers { get; private set; }

        /// <summary>
        /// The vehicle the player is piloting, or null on foot.
        /// </summary>
        public ItemNode Piloting { get; private set; }

        /// <summary>
        /// The money container; its amount is the character's money.
        /// </summary>
        public ItemNode Money { get { return Containers.Find(c => c.Name == "money"); } }

        /// <summary>
        /// The credit container: the money in the bank (0x19 moves money between the two).
        /// </summary>
        public ItemNode Credit { get { return Containers.Find(c => c.Name == "credit"); } }

        /// <summary>
        /// container_id of the row that stores the credit (item_amount), as the characters table has no column
        /// for it. Not a real container's static id.
        /// </summary>
        public const int CreditRow = 500001;

        public PlayerInventory(Character character, IEnumerable<CharacterItem> items)
        {
            Containers = new List<ItemNode>();

            foreach (var container in PlayerContainers.PlayerInfoList)
            {
                var node = new ItemNode(PlayerContainers.GetUniqueID(character.ClientID, container), container.Item3, container.Item2)
                {
                    Name = container.Item1,
                };
                if (container.Item1 == "money")
                {
                    node.Amount = character.Money;
                }
                Containers.Add(node);
                Register(node);
            }

            // Weared has fixed slots (UCGOZone-Login.pcap): slot 0 holds the vehicle being piloted
            // (empty: static id -1), then one slot per piece of clothing. The client reads the slots by
            // position, so empty ones are sent too.
            var weared = Find(PlayerContainers.Weared);
            weared.Add(ItemNode.EmptySlot(-1));
            foreach (var type in WearedSlots)
            {
                var apparel = character.GetApparel(type);
                if (apparel.ItemID > 0)
                {
                    Register(weared.Add(NewItem(apparel.ItemID, 1, type.ToString().ToLowerInvariant())));
                }
                else
                {
                    weared.Add(ItemNode.EmptySlot(0));
                }
            }

            // The official swap pack held a trade pack container, which the client asks for.
            var swapPack = Find(PlayerContainers.SwapPack);
            if (swapPack != null)
            {
                Register(swapPack.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, PlayerContainers.TradePack) { Name = "tradepack" }));
            }

            foreach (var item in items)
            {
                if (item.ContainerID == CreditRow)
                {
                    Credit.Amount = item.Amount;
                    continue;
                }

                var container = Find(item.ContainerID);
                if (container == null || item.ItemID <= 0 || item.ContainerID == PlayerContainers.Weared)
                {
                    Logger.ShowWarning(string.Format("{0}: skipping item {1} ({2}) in unknown container {3}.",
                        character.Name, item.ItemID, item.Name, item.ContainerID));
                    kept.Add(item);
                    continue;
                }

                if (item.ContainerID == PlayerContainers.Hangar)
                {
                    AddVehicle(container, item);
                }
                else
                {
                    Register(container.Add(NewItem(item.ItemID, item.Amount > 0 ? item.Amount : 1, item.Name)));
                }
            }
        }

        /// <summary>
        /// The container or item with this unique id, or null.
        /// </summary>
        public ItemNode Get(uint uniqueID)
        {
            lock (sync)
            {
                ItemNode node;
                nodes.TryGetValue(uniqueID, out node);
                return node;
            }
        }

        /// <summary>
        /// The top-level container with this static id, or null.
        /// </summary>
        public ItemNode Find(int staticID)
        {
            return Containers.Find(c => c.StaticID == staticID);
        }

        /// <summary>
        /// Takes a vehicle out of the hangar and puts the player in it (0x17 section 8; Java:
        /// MoveItem.hangarRideLogic). Returns null when it cannot.
        /// </summary>
        public ItemNode Ride(uint vehicleUID, uint hangarUID)
        {
            lock (sync)
            {
                var vehicle = GetLocked(vehicleUID);
                var hangar = GetLocked(hangarUID);
                if (Piloting != null || vehicle == null || hangar == null || vehicle.Parent != hangar ||
                    hangar.StaticID != PlayerContainers.Hangar)
                {
                    return null;
                }

                hangar.Remove(vehicle);
                Find(PlayerContainers.Weared).SetSlot(PilotSlot, vehicle);
                Piloting = vehicle;
                return vehicle;
            }
        }

        /// <summary>
        /// Puts the piloted vehicle back in the hangar (0x17 section 9; Java: MoveItem.hangarPutLogic).
        /// Returns null when it cannot.
        /// </summary>
        public ItemNode PutBack(uint vehicleUID, uint hangarUID)
        {
            lock (sync)
            {
                var hangar = GetLocked(hangarUID);
                var vehicle = Piloting;
                if (vehicle == null || vehicle.UniqueID != vehicleUID || hangar == null ||
                    hangar.StaticID != PlayerContainers.Hangar)
                {
                    return null;
                }

                Find(PlayerContainers.Weared).SetSlot(PilotSlot, ItemNode.EmptySlot(-1));
                hangar.Add(vehicle);
                Piloting = null;
                return vehicle;
            }
        }

        /// <summary>
        /// Moves <paramref name="amount"/> of an item between two of the player's containers (0x17 section 7;
        /// Java: MoveItem.bankLogic). Stacks of the same item merge, as the official server did.
        /// </summary>
        public MoveResult Move(uint itemUID, uint sourceUID, uint destUID, int amount)
        {
            lock (sync)
            {
                var item = GetLocked(itemUID);
                var source = GetLocked(sourceUID);
                var dest = GetLocked(destUID);
                if (item == null || source == null || dest == null || item.Parent != source || source == dest ||
                    dest.Format != ItemNode.Multi || !CanHoldItems(source) || !CanHoldItems(dest) ||
                    item.Format != ItemNode.Singleton || amount <= 0 || amount > item.Amount)
                {
                    return null;
                }

                bool movingAll = amount >= item.Amount;
                var stack = dest.Children.Find(c => c.StaticID == item.StaticID && c.Format == ItemNode.Singleton);
                var result = new MoveResult { Item = item };

                if (stack != null)
                {
                    // Official: 0x0301 when the whole stack joins the one there, 0x0401 for part of it.
                    stack.Amount += amount;
                    Touch(stack);
                    result.Target = stack;
                    if (movingAll)
                    {
                        source.Remove(item);
                        Unregister(item);
                        result.Kind = MoveKind.Merged;
                    }
                    else
                    {
                        item.Amount -= amount;
                        Touch(item);
                        result.Kind = MoveKind.AddedToStack;
                    }
                }
                else if (movingAll)
                {
                    source.Remove(item);
                    dest.Add(item);
                    Touch(item);
                    result.Kind = MoveKind.Moved;
                }
                else
                {
                    // Official 0x0201: part of a stack becomes a new item in the destination.
                    item.Amount -= amount;
                    Touch(item);
                    var created = NewItem(item.StaticID, amount, item.Name);
                    Register(dest.Add(created));
                    result.Target = created;
                    result.Kind = MoveKind.Split;
                }

                return result;
            }
        }

        /// <summary>
        /// Buys <paramref name="amount"/> of an item into a container (0x21), paying <paramref name="price"/>
        /// from the money. Items that stack join a stack of the same item there (<see cref="BuyResult.Added"/>).
        /// Vehicles go in the hangar, or into weared's vehicle slot to ride away at once (cars and shuttles).
        /// Returns null when the container cannot take it or the money is short.
        /// </summary>
        public BuyResult Buy(ItemTemplate template, uint destUID, int amount, int price)
        {
            lock (sync)
            {
                var dest = GetLocked(destUID);
                var money = Money;
                if (dest == null || amount <= 0 || price < 0 || price > money.Amount)
                {
                    return null;
                }

                ItemNode item;
                bool added = false;
                if (template.IsVehicle)
                {
                    bool toHangar = dest.StaticID == PlayerContainers.Hangar && dest.Parent == null;
                    bool toWeared = dest.StaticID == PlayerContainers.Weared && Piloting == null;
                    if (amount != 1 || !(toHangar || toWeared))
                    {
                        return null;
                    }
                    item = NewVehicle(new CharacterItem { ItemID = template.ID, Name = template.Name });
                    if (toWeared)
                    {
                        dest.SetSlot(PilotSlot, item);
                        Piloting = item;
                    }
                    else
                    {
                        dest.Add(item);
                    }
                }
                else
                {
                    if (!CanHoldItems(dest))
                    {
                        return null;
                    }
                    item = template.Stacks
                        ? dest.Children.Find(c => c.StaticID == template.ID && c.Format == ItemNode.Singleton)
                        : null;
                    if (item != null)
                    {
                        item.Amount += amount;
                        Touch(item);
                        added = true;
                    }
                    else
                    {
                        item = Register(dest.Add(NewItem(template.ID, amount, template.Name)));
                    }
                }

                money.Amount -= price;
                return new BuyResult { Item = item, Added = added };
            }
        }

        /// <summary>
        /// Sells <paramref name="amount"/> of an item, or a vehicle from the hangar (0x22), for
        /// <paramref name="unitPrice"/> each. Returns null when it cannot, otherwise whether the whole stack went.
        /// </summary>
        public bool? Sell(uint itemUID, uint containerUID, int amount, int unitPrice)
        {
            lock (sync)
            {
                var item = GetLocked(itemUID);
                var container = GetLocked(containerUID);
                if (item == null || container == null || item.Parent != container || amount <= 0)
                {
                    return null;
                }

                bool vehicle = container.StaticID == PlayerContainers.Hangar && container.Parent == null;
                if (!vehicle && (!CanHoldItems(container) || item.Format != ItemNode.Singleton || amount > item.Amount))
                {
                    return null;
                }

                bool all = vehicle || amount >= item.Amount;
                if (all)
                {
                    container.Remove(item);
                    UnregisterTree(item);
                }
                else
                {
                    item.Amount -= amount;
                    Touch(item);
                }
                Money.Amount += unitPrice * (vehicle ? 1 : amount);
                return all;
            }
        }

        /// <summary>
        /// Throws an item away (0x15): the piloted vehicle (the player is then on foot) or an item in one of the
        /// containers. Returns what was deleted, or null.
        /// </summary>
        public ItemNode Delete(uint itemUID, uint containerUID)
        {
            lock (sync)
            {
                if (Piloting != null && Piloting.UniqueID == itemUID)
                {
                    var vehicle = Piloting;
                    Find(PlayerContainers.Weared).SetSlot(PilotSlot, ItemNode.EmptySlot(-1));
                    UnregisterTree(vehicle);
                    Piloting = null;
                    return vehicle;
                }

                var item = GetLocked(itemUID);
                var container = GetLocked(containerUID);
                if (item == null || container == null || item.Parent != container ||
                    !(CanHoldItems(container) || (container.StaticID == PlayerContainers.Hangar && container.Parent == null)))
                {
                    return null;
                }
                container.Remove(item);
                UnregisterTree(item);
                return item;
            }
        }

        /// <summary>
        /// Puts the player back in the vehicle they flew in with (a shuttle between Earth and Space): it keeps
        /// its unique id, which the client goes on using.
        /// </summary>
        public void Resume(ItemNode vehicle)
        {
            lock (sync)
            {
                if (Piloting != null)
                {
                    return;
                }
                Find(PlayerContainers.Weared).SetSlot(PilotSlot, vehicle);
                RegisterTree(vehicle);
                Piloting = vehicle;
            }
        }

        /// <summary>
        /// Slots of a vehicle's armaments container: 0 main weapon, 1 shield or left hand, higher ones further
        /// weapons (the official ACGUY had 4). Empty slots are sent as 0, 0, 0.
        /// </summary>
        public const int MaxArmamentSlots = 8;

        /// <summary>
        /// Equips a weapon or shield from the piloted vehicle's inventory into armament slot
        /// <paramref name="slot"/> (0x1B); what was there goes back to the inventory.
        /// </summary>
        public bool Equip(uint itemUID, int slot)
        {
            lock (sync)
            {
                var vehicle = Piloting;
                var item = GetLocked(itemUID);
                if (vehicle == null || item == null || !item.IsEquipment || slot < 0 || slot >= MaxArmamentSlots)
                {
                    return false;
                }
                var inventory = vehicle.Children.Find(c => c.StaticID == VehicleInventory);
                var armaments = vehicle.Children.Find(c => c.StaticID == VehicleArmaments);
                if (inventory == null || armaments == null || item.Parent != inventory)
                {
                    return false;
                }

                inventory.Remove(item);
                PadSlots(armaments, slot);
                var old = armaments.SetSlot(slot, item);
                if (!old.IsEmptySlot)
                {
                    inventory.Add(old);
                }
                return true;
            }
        }

        /// <summary>
        /// Takes what is in armament slot <paramref name="slot"/> back to the piloted vehicle's inventory (0x1B).
        /// </summary>
        public bool Unequip(int slot)
        {
            lock (sync)
            {
                var vehicle = Piloting;
                if (vehicle == null)
                {
                    return false;
                }
                var inventory = vehicle.Children.Find(c => c.StaticID == VehicleInventory);
                var armaments = vehicle.Children.Find(c => c.StaticID == VehicleArmaments);
                if (inventory == null || armaments == null || slot < 0 || slot >= armaments.Children.Count ||
                    armaments.Children[slot].IsEmptySlot)
                {
                    return true;
                }
                inventory.Add(armaments.SetSlot(slot, ItemNode.EmptySlot(0)));
                while (armaments.Children.Count > 0 && armaments.Children[armaments.Children.Count - 1].IsEmptySlot)
                {
                    armaments.Children.RemoveAt(armaments.Children.Count - 1);
                }
                return true;
            }
        }

        /// <summary>
        /// The weapon in armament slot <paramref name="slot"/> of the piloted vehicle, or null.
        /// </summary>
        public ItemNode Armament(int slot)
        {
            lock (sync)
            {
                var armaments = Piloting != null ? Piloting.Children.Find(c => c.StaticID == VehicleArmaments) : null;
                if (armaments == null || slot < 0 || slot >= armaments.Children.Count || armaments.Children[slot].IsEmptySlot)
                {
                    return null;
                }
                return armaments.Children[slot];
            }
        }

        /// <summary>
        /// The template of each armament slot of a vehicle, -1 for an empty one (for its looks, 0x800A).
        /// </summary>
        public static int[] ArmamentTemplates(ItemNode vehicle)
        {
            var armaments = vehicle != null ? vehicle.Children.Find(c => c.StaticID == VehicleArmaments) : null;
            if (armaments == null)
            {
                return new int[0];
            }
            return armaments.Children.ConvertAll(c => c.IsEmptySlot ? -1 : c.StaticID).ToArray();
        }

        /// <summary>
        /// Moves <paramref name="rounds"/> from an ammunition stack into a weapon (0x1D).
        /// </summary>
        public bool Reload(uint ammoUID, uint containerUID, uint weaponUID, int rounds)
        {
            lock (sync)
            {
                var ammo = GetLocked(ammoUID);
                var container = GetLocked(containerUID);
                var weapon = GetLocked(weaponUID);
                if (ammo == null || weapon == null || container == null || ammo.Parent != container ||
                    !weapon.IsEquipment || rounds <= 0 || rounds > ammo.Amount)
                {
                    return false;
                }
                ammo.Amount -= rounds;
                if (ammo.Amount == 0)
                {
                    container.Remove(ammo);
                    Unregister(ammo);
                }
                weapon.Loaded += rounds;
                return true;
            }
        }

        /// <summary>
        /// Removes a destroyed weapon or shield from armament slot <paramref name="slot"/>.
        /// </summary>
        public void DestroyArmament(int slot)
        {
            lock (sync)
            {
                var armaments = Piloting != null ? Piloting.Children.Find(c => c.StaticID == VehicleArmaments) : null;
                if (armaments == null || slot < 0 || slot >= armaments.Children.Count || armaments.Children[slot].IsEmptySlot)
                {
                    return;
                }
                UnregisterTree(armaments.SetSlot(slot, ItemNode.EmptySlot(0)));
            }
        }

        /// <summary>
        /// Share of the maximum health an ER kit repairs (the official one repaired 1360 of 4000 in
        /// Self_ER_REpair.pcap).
        /// </summary>
        public const int RepairKitPercent = 34;

        /// <summary>
        /// Uses one ER kit (31xxxx) from <paramref name="containerUID"/> on the piloted vehicle (0x1C). Returns the
        /// health repaired, or -1 when there is no kit, no vehicle or nothing to repair.
        /// </summary>
        public int UseRepairKit(uint itemUID, uint containerUID)
        {
            lock (sync)
            {
                var vehicle = Piloting;
                var kit = GetLocked(itemUID);
                var container = GetLocked(containerUID);
                if (vehicle == null || kit == null || container == null || kit.Parent != container ||
                    kit.StaticID / 10000 != 31 || kit.Amount <= 0 || vehicle.MaxHealth <= 0 ||
                    vehicle.Health <= 0 || vehicle.Health >= vehicle.MaxHealth)
                {
                    return -1;
                }
                int repaired = System.Math.Min(vehicle.MaxHealth - vehicle.Health, vehicle.MaxHealth * RepairKitPercent / 100);
                SetHealth(vehicle, vehicle.Health + repaired);
                kit.Amount--;
                if (kit.Amount == 0)
                {
                    container.Remove(kit);
                    Unregister(kit);
                }
                return repaired;
            }
        }

        /// <summary>
        /// Takes the destroyed piloted vehicle away from the player, who is then on foot. Returns it, or null.
        /// </summary>
        public ItemNode LoseVehicle()
        {
            return GetOff(Piloting != null ? Piloting.UniqueID : 0);
        }

        private static void PadSlots(ItemNode container, int slot)
        {
            while (container.Children.Count <= slot)
            {
                container.Add(ItemNode.EmptySlot(0));
            }
        }

        /// <summary>
        /// Moves money between the money container and the bank (0x19). False when the source is short.
        /// </summary>
        public bool TransferMoney(bool toBank, int amount)
        {
            lock (sync)
            {
                var from = toBank ? Money : Credit;
                var to = toBank ? Credit : Money;
                if (amount <= 0 || amount > from.Amount)
                {
                    return false;
                }
                from.Amount -= amount;
                to.Amount += amount;
                return true;
            }
        }

        /// <summary>
        /// Repairs a vehicle to full health (0x18), paying <paramref name="price"/>. Returns false when the
        /// vehicle is not the player's or the money is short.
        /// </summary>
        public bool Repair(uint vehicleUID, int price)
        {
            lock (sync)
            {
                var vehicle = GetLocked(vehicleUID);
                if (vehicle == null || vehicle.Format != ItemNode.Multi || vehicle.MaxHealth <= 0 || price > Money.Amount)
                {
                    return false;
                }
                Money.Amount -= price;
                SetHealth(vehicle, vehicle.MaxHealth);
                return true;
            }
        }

        /// <summary>
        /// Sets a vehicle's health, in its node and its stats field.
        /// </summary>
        public static void SetHealth(ItemNode vehicle, int health)
        {
            vehicle.Health = health;
            var stats = vehicle.Options != null && vehicle.Options.Length > 5 ? vehicle.Options[5] : null;
            if (stats != null && stats.Length >= 5)
            {
                stats[1] = (byte)(health >> 24);
                stats[2] = (byte)(health >> 16);
                stats[3] = (byte)(health >> 8);
                stats[4] = (byte)health;
            }
        }

        /// <summary>
        /// Takes <paramref name="amount"/> of an item out of a container to drop it on the ground (0x23 item).
        /// Money is dropped from the money container itself (the client sends container 0). Returns the new
        /// ground item (the official server gave it a new unique id), or null when it cannot.
        /// </summary>
        public ItemNode TakeForDrop(uint itemUID, uint containerUID, int amount)
        {
            lock (sync)
            {
                var item = GetLocked(itemUID);
                if (item == null || amount <= 0 || item.Format != ItemNode.Singleton || amount > item.Amount)
                {
                    return null;
                }

                if (item == Money)
                {
                    item.Amount -= amount;
                    return NewItem(PlayerContainers.Money, amount, "money");
                }

                var container = GetLocked(containerUID);
                if (container == null || item.Parent != container || !CanHoldItems(container))
                {
                    return null;
                }

                if (amount >= item.Amount)
                {
                    container.Remove(item);
                    Unregister(item);
                }
                else
                {
                    item.Amount -= amount;
                    Touch(item);
                }
                var dropped = NewItem(item.StaticID, amount, item.Name);
                dropped.Created = item.Created;
                return dropped;
            }
        }

        /// <summary>
        /// Gets out of the piloted vehicle, leaving it on the ground (0x23 mini op 2). The vehicle keeps its
        /// unique id. Returns null when the player is not piloting it.
        /// </summary>
        public ItemNode GetOff(uint vehicleUID)
        {
            lock (sync)
            {
                var vehicle = Piloting;
                if (vehicle == null || vehicle.UniqueID != vehicleUID)
                {
                    return null;
                }
                Find(PlayerContainers.Weared).SetSlot(PilotSlot, ItemNode.EmptySlot(-1));
                UnregisterTree(vehicle);
                Piloting = null;
                return vehicle;
            }
        }

        /// <summary>
        /// Puts an item picked up from the ground in a container (0x24 item). Money goes to the money
        /// container. False when the container cannot take it.
        /// </summary>
        public bool PickUp(ItemNode item, uint destUID)
        {
            lock (sync)
            {
                if (item.StaticID == PlayerContainers.Money && item.Format == ItemNode.Singleton)
                {
                    Money.Amount += item.Amount;
                    return true;
                }

                var dest = GetLocked(destUID);
                if (dest == null || !CanHoldItems(dest))
                {
                    return false;
                }
                Touch(item);
                dest.Add(item);
                RegisterTree(item);
                return true;
            }
        }

        /// <summary>
        /// Gets in a vehicle on the ground (0x24 mini op 3); it goes in weared's vehicle slot.
        /// </summary>
        public bool Board(ItemNode vehicle, uint wearedUID)
        {
            lock (sync)
            {
                var weared = Find(PlayerContainers.Weared);
                if (Piloting != null || weared == null || weared.UniqueID != wearedUID)
                {
                    return false;
                }
                weared.SetSlot(PilotSlot, vehicle);
                RegisterTree(vehicle);
                Piloting = vehicle;
                return true;
            }
        }

        /// <summary>
        /// The rows to write back to the container table: every item in the top-level containers except
        /// weared (clothes are in the garments table). A piloted vehicle, and the player's vehicles left on
        /// the ground (<paramref name="groundVehicles"/>), are saved in the hangar, with their inventories.
        /// </summary>
        public List<CharacterItem> ToRows(IEnumerable<ItemNode> groundVehicles = null, bool skipPiloting = false)
        {
            var rows = new List<CharacterItem>();
            lock (sync)
            {
                foreach (var container in Containers)
                {
                    if (container.StaticID == PlayerContainers.Weared || container.Format != ItemNode.Multi)
                    {
                        continue;
                    }
                    foreach (var item in container.Children)
                    {
                        if (item.StaticID == PlayerContainers.TradePack && container.StaticID == PlayerContainers.SwapPack)
                        {
                            continue;
                        }
                        rows.Add(ToRow(container.StaticID, item));
                    }
                }

                if (Piloting != null && !skipPiloting)
                {
                    rows.Add(ToRow(PlayerContainers.Hangar, Piloting));
                }
                if (groundVehicles != null)
                {
                    foreach (var vehicle in groundVehicles)
                    {
                        rows.Add(ToRow(PlayerContainers.Hangar, vehicle));
                    }
                }
                if (Credit.Amount != 0)
                {
                    rows.Add(new CharacterItem { ContainerID = CreditRow, ItemID = PlayerContainers.Credit, Name = "credit", Amount = Credit.Amount });
                }
                rows.AddRange(kept);
            }
            return rows;
        }

        private static CharacterItem ToRow(int containerID, ItemNode item)
        {
            bool vehicle = containerID == PlayerContainers.Hangar;
            return new CharacterItem
            {
                ContainerID = containerID,
                ItemID = item.StaticID,
                Name = item.Name,
                Amount = vehicle ? item.EngineID : item.Amount,
                Children = vehicle ? VehicleChildren(item) : null,
            };
        }

        /// <summary>
        /// Containers items can be moved in and out of with section 7: the player's own top-level list
        /// containers other than weared and the hangar (those have their own sections), and vehicle
        /// inventories (saved in container.child). The trade pack is not saved yet, so moves into it are refused.
        /// </summary>
        private static bool CanHoldItems(ItemNode container)
        {
            if (container.Format != ItemNode.Multi)
            {
                return false;
            }
            if (container.StaticID == VehicleInventory)
            {
                return container.Parent != null;
            }
            return container.Parent == null &&
                container.StaticID != PlayerContainers.Weared && container.StaticID != PlayerContainers.Hangar;
        }

        private void AddVehicle(ItemNode hangar, CharacterItem item)
        {
            hangar.Add(NewVehicle(item));
        }

        /// <summary>
        /// A vehicle with its armaments and inventory, registered. <paramref name="item"/>.Amount is the engine
        /// id (0 or -1: the template's).
        /// </summary>
        private ItemNode NewVehicle(CharacterItem item)
        {
            var template = VehicleTemplates.Get(item.ItemID);
            int health = template != null ? template.Health : VehicleTemplates.DefaultHealth;
            int engine = item.Amount > 0 ? item.Amount : (template != null ? template.EngineID : VehicleTemplates.DefaultEngine);

            var vehicle = new ItemNode(NewUniqueID(), ItemNode.Multi, item.ItemID)
            {
                Name = !string.IsNullOrEmpty(item.Name) ? item.Name : (template != null ? template.Name : null),
                EngineID = item.Amount > 0 ? item.Amount : -1,
            };
            vehicle.Created = UnixNow();
            vehicle.Health = health;
            vehicle.MaxHealth = health;
            vehicle.Options = new byte[6][];
            vehicle.Options[5] = VehicleStats(health, health, engine);
            Register(vehicle);

            Register(vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleArmaments) { Name = "armaments", Modified = -1, Created = -1 }));
            var inventory = Register(vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleInventory) { Name = "inventory", Modified = -1, Created = -1 }));

            // Java's container.child format: "itemID-amount" separated by spaces; equipped armaments are
            // written "@slot-itemID".
            var armaments = vehicle.Children.Find(c => c.StaticID == VehicleArmaments);
            foreach (var entry in (item.Children ?? string.Empty).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                bool armament = entry.StartsWith("@");
                var parts = entry.TrimStart('@').Split('-');
                int first, second;
                if (parts.Length != 2 || !int.TryParse(parts[0], out first) || !int.TryParse(parts[1], out second))
                {
                    continue;
                }
                if (armament && first >= 0 && first < MaxArmamentSlots && second > 0)
                {
                    PadSlots(armaments, first);
                    armaments.SetSlot(first, Register(NewItem(second, 1, null)));
                }
                else if (!armament && first > 0)
                {
                    Register(inventory.Add(NewItem(first, second > 0 ? second : 1, null)));
                }
            }
            return vehicle;
        }

        /// <summary>
        /// The container.child value for a vehicle: the items in its inventory.
        /// </summary>
        private static string VehicleChildren(ItemNode vehicle)
        {
            var entries = new List<string>();
            var inventory = vehicle.Children.Find(c => c.StaticID == VehicleInventory);
            if (inventory != null)
            {
                entries.AddRange(inventory.Children.ConvertAll(c => c.StaticID + "-" + c.Amount));
            }
            var armaments = vehicle.Children.Find(c => c.StaticID == VehicleArmaments);
            if (armaments != null)
            {
                for (int i = 0; i < armaments.Children.Count; i++)
                {
                    if (!armaments.Children[i].IsEmptySlot)
                    {
                        entries.Add("@" + i + "-" + armaments.Children[i].StaticID);
                    }
                }
            }
            return string.Join(" ", entries);
        }

        /// <summary>
        /// The vehicle's stats field: 19 ints. The official TGM-79 had 3220 of 3300 health and its engine id
        /// in the 18th (UCGOZone-Login.pcap); the other values are the Java server's constants
        /// (Vehicle.write), which it had not decoded.
        /// </summary>
        private static byte[] VehicleStats(int health, int maxHealth, int engineID)
        {
            var values = new[]
            {
                health, 0x7D0, 0x9C4, 0x9C4, 0xBB8, 0xBB8,
                maxHealth, 0x7D0, 0x9C4, 0x9C4, 0xBB8, 0xBB8,
                0x4B, 0x3E8, 0x19, 0x14, 0, engineID, 0,
            };

            var bytes = new byte[1 + values.Length * 4];
            bytes[0] = (byte)(0x80 | values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                bytes[1 + i * 4] = (byte)(values[i] >> 24);
                bytes[2 + i * 4] = (byte)(values[i] >> 16);
                bytes[3 + i * 4] = (byte)(values[i] >> 8);
                bytes[4 + i * 4] = (byte)values[i];
            }
            return bytes;
        }

        public static ItemNode NewItem(int templateID, int amount, string name)
        {
            int now = UnixNow();
            var item = new ItemNode(NewUniqueID(), ItemNode.Singleton, templateID)
            {
                Amount = amount,
                Created = now,
                Modified = now,
                Name = name,
            };

            // Weapons and shields come loaded and at full durability (the official 75mm machine gun was bought
            // with 100 rounds in it).
            var template = ItemTemplates.Get(templateID);
            if (template != null && (template.IsWeapon || template.IsShield) && template.Durability > 0)
            {
                item.Stats = template.IsWeapon
                    ? new[] { template.Durability, template.Durability, template.Power, template.Rate, template.Range, 0, 1000 }
                    : new[] { template.Durability, template.Durability, 0, 0, 0, 0, 1000 };
                item.Loaded = template.IsWeapon ? template.Magazine : 0;
            }
            return item;
        }

        private static void Touch(ItemNode item)
        {
            item.Modified = UnixNow();
        }

        private ItemNode GetLocked(uint uniqueID)
        {
            ItemNode node;
            nodes.TryGetValue(uniqueID, out node);
            return node;
        }

        private ItemNode Register(ItemNode node)
        {
            lock (sync)
            {
                nodes[node.UniqueID] = node;
            }
            return node;
        }

        private void Unregister(ItemNode node)
        {
            nodes.Remove(node.UniqueID);
        }

        private void RegisterTree(ItemNode node)
        {
            foreach (var n in node.Descendants())
            {
                if (!n.IsEmptySlot)
                {
                    nodes[n.UniqueID] = n;
                }
            }
        }

        private void UnregisterTree(ItemNode node)
        {
            foreach (var n in node.Descendants())
            {
                if (!n.IsEmptySlot)
                {
                    nodes.Remove(n.UniqueID);
                }
            }
        }

        /// <summary>
        /// A new unique id for an item, from the server-wide counter (also used for items on the ground).
        /// </summary>
        public static uint NewUniqueID()
        {
            return (uint)Interlocked.Increment(ref nextUniqueID);
        }

        private static int UnixNow()
        {
            return GameWorld.UnixTime();
        }
    }

    public enum MoveKind
    {
        /// <summary>The item changed containers (official reply 0x0101).</summary>
        Moved,

        /// <summary>Part of a stack became a new item in the destination (0x0201).</summary>
        Split,

        /// <summary>The whole stack joined a stack of the same item (0x0301).</summary>
        Merged,

        /// <summary>Part of a stack joined a stack of the same item (0x0401).</summary>
        AddedToStack,
    }

    public class BuyResult
    {
        /// <summary>The new item, or the stack the bought items joined.</summary>
        public ItemNode Item { get; set; }

        /// <summary>True when they joined a stack (official reply 0x01), false for a new item (0x02).</summary>
        public bool Added { get; set; }
    }

    public class MoveResult
    {
        public MoveKind Kind { get; set; }

        /// <summary>The item that was moved (for Merged it no longer exists).</summary>
        public ItemNode Item { get; set; }

        /// <summary>The destination stack (Merged, AddedToStack) or the new item (Split).</summary>
        public ItemNode Target { get; set; }
    }
}
