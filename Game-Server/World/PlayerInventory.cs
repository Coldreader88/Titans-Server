using System;
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

        /// <summary>
        /// container_id of the row that stores the vehicle the character was piloting when they left (a shuttle
        /// in flight too), so they log in again sitting in it. Not a real container's static id.
        /// </summary>
        public const int PilotingRow = 500002;

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
                if (item.ContainerID == PilotingRow)
                {
                    if (Piloting == null && item.ItemID > 0)
                    {
                        var vehicle = NewVehicle(item);
                        weared.SetSlot(PilotSlot, vehicle);
                        Piloting = vehicle;
                    }
                    continue;
                }

                var container = item.ContainerID == PlayerContainers.TradePack ? TradePack : Find(item.ContainerID);
                if (container == null || item.ItemID <= 0 || item.ContainerID == PlayerContainers.Weared)
                {
                    Logger.ShowWarning(string.Format("{0}: skipping item {1} ({2}) in unknown container {3}.",
                        character.Name, item.ItemID, item.Name, item.ContainerID));
                    kept.Add(item);
                    continue;
                }

                var itemTemplate = ItemTemplates.Get(item.ItemID);
                if (item.ContainerID == PlayerContainers.Hangar || (itemTemplate != null && itemTemplate.IsVehicle))
                {
                    // Vehicles also wait in the productive container (the factory) after they are built.
                    AddVehicle(container, item);
                }
                else
                {
                    Register(container.Add(ApplyState(NewItem(item.ItemID, item.Amount > 0 ? item.Amount : 1, item.Name), item.Children)));
                }
            }
        }

        /// <summary>
        /// The trade pack inside the swap pack, or null.
        /// </summary>
        public ItemNode TradePack
        {
            get
            {
                var swapPack = Find(PlayerContainers.SwapPack);
                return swapPack != null ? swapPack.Children.Find(c => c.StaticID == PlayerContainers.TradePack) : null;
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
                if (item != null && source != null && dest != null && item.Parent == source && IsVehicle(item) &&
                    HoldsVehicles(source) && HoldsVehicles(dest) && source != dest)
                {
                    // A vehicle built in the factory goes to the hangar ("take out"), or back in to be taken apart
                    // (Zaku_F2A_Factory_to_Hangar.pcap: section 7, answered like an item move).
                    source.Remove(item);
                    dest.Add(item);
                    return new MoveResult { Item = item, Kind = MoveKind.Moved };
                }
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
        /// A quest hand-in (0x3E), all or nothing: checks that the offered items (their unique ids) cover every
        /// required template and amount, takes those amounts, adds the money and puts the reward item in the
        /// backpack. Null with <paramref name="refusal"/> set when it cannot; otherwise the items used: each
        /// with the amount taken and whether it is gone.
        /// </summary>
        public List<QuestUse> HandIn(IEnumerable<uint> offered, IList<KeyValuePair<int, int>> required, int money, ItemNode reward,
            out ItemNode rewardContainer, out string refusal)
        {
            rewardContainer = null;
            lock (sync)
            {
                var items = new List<ItemNode>();
                foreach (var uid in offered)
                {
                    var item = GetLocked(uid);
                    if (item != null && !items.Contains(item) && item.Parent != null && CanHoldItems(item.Parent) && !IsVehicle(item))
                    {
                        items.Add(item);
                    }
                }

                var needs = new Dictionary<int, int>();
                foreach (var r in required)
                {
                    int n;
                    needs.TryGetValue(r.Key, out n);
                    needs[r.Key] = n + Math.Max(1, r.Value);
                }
                foreach (var need in needs)
                {
                    int have = 0;
                    foreach (var item in items)
                    {
                        if (item.StaticID == need.Key)
                        {
                            have += Math.Max(1, item.Amount);
                        }
                    }
                    if (have < need.Value)
                    {
                        refusal = string.Format("{0} of {1} handed in, {2} needed", have, need.Key, need.Value);
                        return null;
                    }
                }

                if (reward != null)
                {
                    rewardContainer = Find(PlayerContainers.Backpack);
                    if (rewardContainer == null || !CanHoldItems(rewardContainer))
                    {
                        refusal = "no backpack for the reward";
                        return null;
                    }
                }

                var used = new List<QuestUse>();
                foreach (var need in needs)
                {
                    int left = need.Value;
                    foreach (var item in items)
                    {
                        if (left == 0 || item.StaticID != need.Key)
                        {
                            continue;
                        }
                        int amount = Math.Max(1, item.Amount);
                        int take = Math.Min(left, amount);
                        left -= take;
                        if (take < amount)
                        {
                            item.Amount -= take;
                            Touch(item);
                            used.Add(new QuestUse(item, take, false));
                        }
                        else
                        {
                            item.Parent.Remove(item);
                            UnregisterTree(item);
                            used.Add(new QuestUse(item, take, true));
                        }
                    }
                }

                if (money != 0 && Money != null)
                {
                    Money.Amount += money;
                }
                if (reward != null)
                {
                    Touch(reward);
                    rewardContainer.Add(reward);
                    RegisterTree(reward);
                }
                refusal = null;
                return used;
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
        /// <paramref name="slot"/> (0x1B); what was there goes back to the inventory. Refused when the vehicle
        /// cannot carry that kind of item in that slot (<see cref="VehicleEquipment"/>).
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
                if (inventory == null || armaments == null || item.Parent != inventory ||
                    !VehicleEquipment.CanEquip(vehicle.StaticID, slot, item.StaticID))
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
                if (item != null && IsVehicle(item) && item.Parent != null && item.Parent.UniqueID == containerUID &&
                    IsFactory(item.Parent))
                {
                    // Dragged out of the factory onto the ground; it keeps its unique id
                    // (Zaku_F2A_Drag_Out_of_Factory.pcap).
                    item.Parent.Remove(item);
                    UnregisterTree(item);
                    return item;
                }
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
                if (IsVehicle(item))
                {
                    // Dragged from the ground into the factory, to be taken apart (Zaku_F2A_Drag_into_Factory.pcap).
                    if (dest == null || !IsFactory(dest))
                    {
                        return false;
                    }
                    dest.Add(item);
                    RegisterTree(item);
                    return true;
                }
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
        /// Picks an item up onto the stack of the same item already in the destination (0x24 mini op 2 for
        /// money, 4 for other items; Backpack_Pickup_11222_EF.pcap): the ground item joins that stack and is
        /// gone. Returns the stack, or null when there is none to join.
        /// </summary>
        public ItemNode PickUpOntoStack(ItemNode item, uint destUID)
        {
            lock (sync)
            {
                if (item.Format != ItemNode.Singleton || IsVehicle(item))
                {
                    return null;
                }
                var dest = GetLocked(destUID);
                ItemNode stack = null;
                if (dest != null && CanHoldItems(dest))
                {
                    stack = dest.Children.Find(c => c.StaticID == item.StaticID && c.Format == ItemNode.Singleton);
                }
                if (stack == null && item.StaticID == PlayerContainers.Money && Money != null)
                {
                    stack = Money;
                }
                if (stack == null)
                {
                    return null;
                }
                stack.Amount += item.Amount;
                Touch(stack);
                return stack;
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
        /// weared (clothes are in the garments table), and what is in the trade pack (container_id 110005).
        /// A worn weapon or shield keeps its durability and loaded rounds in the child column
        /// (<see cref="StateOf"/>). The piloted vehicle has its own row (<see cref="PilotingRow"/>); vehicles left
        /// on the ground are saved with the world (WorldDatabase).
        /// </summary>
        public List<CharacterItem> ToRows()
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
                            // The trade pack itself is made at login; what is in it has rows of its own.
                            foreach (var packed in item.Children)
                            {
                                rows.Add(ToRow(PlayerContainers.TradePack, packed));
                            }
                            continue;
                        }
                        rows.Add(ToRow(container.StaticID, item));
                    }
                }

                if (Piloting != null)
                {
                    rows.Add(ToRow(PilotingRow, Piloting));
                }
                if (Credit.Amount != 0)
                {
                    rows.Add(new CharacterItem { ContainerID = CreditRow, ItemID = PlayerContainers.Credit, Name = "credit", Amount = Credit.Amount });
                }
                rows.AddRange(kept);
            }
            return rows;
        }

        /// <summary>
        /// The saved row of an item or (<paramref name="vehicle"/>) a vehicle with its cargo and armaments.
        /// </summary>
        public static CharacterItem Describe(int containerID, ItemNode item, bool vehicle)
        {
            return new CharacterItem
            {
                ContainerID = containerID,
                ItemID = item.StaticID,
                Name = item.Name,
                Amount = vehicle ? item.EngineID : item.Amount,
                Children = vehicle ? VehicleChildren(item) : StateOf(item),
            };
        }

        private static CharacterItem ToRow(int containerID, ItemNode item)
        {
            bool vehicle = containerID == PlayerContainers.Hangar || containerID == PilotingRow || IsVehicle(item);
            return new CharacterItem
            {
                ContainerID = containerID,
                ItemID = item.StaticID,
                Name = item.Name,
                Amount = vehicle ? item.EngineID : item.Amount,
                Children = vehicle ? VehicleChildren(item) : StateOf(item),
            };
        }

        /// <summary>
        /// Containers items can be moved in and out of with section 7: the player's own top-level list
        /// containers other than weared and the hangar (those have their own sections), and vehicle
        /// inventories (saved in container.child), and the trade pack in the swap pack.
        /// </summary>
        private static bool CanHoldItems(ItemNode container)
        {
            if (container.Format != ItemNode.Multi)
            {
                return false;
            }
            if (container.StaticID == PlayerContainers.TradePack)
            {
                return container.Parent != null && container.Parent.StaticID == PlayerContainers.SwapPack && container.Parent.Parent == null;
            }
            if (container.StaticID == VehicleInventory)
            {
                return container.Parent != null;
            }
            return container.Parent == null &&
                container.StaticID != PlayerContainers.Weared && container.StaticID != PlayerContainers.Hangar;
        }

        /// <summary>
        /// Whether a container is the player's productive container (the factory), where things are made.
        /// </summary>
        public static bool IsFactory(ItemNode container)
        {
            return container != null && container.Parent == null && container.StaticID == PlayerContainers.Productive;
        }

        private static bool HoldsVehicles(ItemNode container)
        {
            return IsFactory(container) || (container.Parent == null && container.StaticID == PlayerContainers.Hangar);
        }

        /// <summary>
        /// Uses up <paramref name="amount"/> of an item in one of the player's containers for production.
        /// Returns 8 when none is left (the item is gone), 9 when some of the stack is, 0 when it cannot
        /// (the states 0x8028 reports).
        /// </summary>
        public uint UseUp(uint itemUID, uint containerUID, int amount)
        {
            lock (sync)
            {
                var item = GetLocked(itemUID);
                if (item == null || item.Parent == null || item.Parent.UniqueID != containerUID || amount <= 0 ||
                    item.Format != ItemNode.Singleton || amount > item.Amount || !CanHoldItems(item.Parent))
                {
                    return 0;
                }
                if (amount >= item.Amount)
                {
                    item.Parent.Remove(item);
                    Unregister(item);
                    return 8;
                }
                item.Amount -= amount;
                Touch(item);
                return 9;
            }
        }

        /// <summary>
        /// Whether one of the player's items could be used up for production: it is in that container, and
        /// the stack has <paramref name="amount"/>.
        /// </summary>
        public bool CanUse(uint itemUID, uint containerUID, int templateID, int amount)
        {
            lock (sync)
            {
                var item = GetLocked(itemUID);
                return item != null && item.StaticID == templateID && item.Parent != null &&
                    item.Parent.UniqueID == containerUID && item.Format == ItemNode.Singleton &&
                    amount > 0 && amount <= item.Amount && CanHoldItems(item.Parent);
            }
        }

        /// <summary>
        /// Puts what production made or gave back in a container: stacking items join a stack of the same item
        /// there, others come one by one (at most <paramref name="amount"/>); a vehicle is built with
        /// <paramref name="engineID"/> (-1: its template's). Returns the (last) node.
        /// </summary>
        public ItemNode Produce(ItemNode container, int templateID, int amount, int engineID = -1)
        {
            lock (sync)
            {
                var template = ItemTemplates.Get(templateID);
                if (template != null && template.IsVehicle)
                {
                    var vehicle = NewVehicle(new CharacterItem { ItemID = templateID, Name = template.Name, Amount = engineID });
                    container.Add(vehicle);
                    return vehicle;
                }
                string name = template != null ? template.Name : null;
                if (template == null || template.Stacks)
                {
                    var stack = container.Children.Find(c => c.StaticID == templateID && c.Format == ItemNode.Singleton);
                    if (stack != null)
                    {
                        stack.Amount += amount;
                        Touch(stack);
                        return stack;
                    }
                    return Register(container.Add(NewItem(templateID, amount, name)));
                }
                ItemNode last = null;
                for (int i = 0; i < amount; i++)
                {
                    last = Register(container.Add(NewItem(templateID, 1, name)));
                }
                return last;
            }
        }

        /// <summary>
        /// Takes a vehicle in the factory apart: it is gone, and its cargo and armaments stay in the factory.
        /// Returns the vehicle, or null when it is not a vehicle in the factory.
        /// </summary>
        public ItemNode Dismantle(uint vehicleUID, uint factoryUID)
        {
            lock (sync)
            {
                var vehicle = GetLocked(vehicleUID);
                var factory = GetLocked(factoryUID);
                if (vehicle == null || !IsVehicle(vehicle) || !IsFactory(factory) || vehicle.Parent != factory)
                {
                    return null;
                }
                factory.Remove(vehicle);
                UnregisterTree(vehicle);
                var parts = new List<ItemNode>();
                foreach (var part in vehicle.Children)
                {
                    parts.AddRange(part.Children.FindAll(c => !c.IsEmptySlot && c.Format == ItemNode.Singleton));
                }
                foreach (var item in parts)
                {
                    // The vehicle is gone, so its weapons and cargo simply change hands, as they are.
                    Register(factory.Add(item));
                }
                return vehicle;
            }
        }

        /// <summary>
        /// Whether a node is a vehicle (a list with its armaments and inventory in it), not a stack of items.
        /// </summary>
        public static bool IsVehicle(ItemNode item)
        {
            return item.Format == ItemNode.Multi && item.Children.Exists(c => c.StaticID == VehicleArmaments);
        }

        private void AddVehicle(ItemNode hangar, CharacterItem item)
        {
            hangar.Add(NewVehicle(item));
        }

        /// <summary>
        /// A vehicle with its armaments and inventory, registered. <paramref name="item"/>.Amount is the engine
        /// id (0 or -1: the template's).
        /// </summary>
        /// <summary>
        /// A new, empty vehicle that belongs to no container yet (#spawn puts it on the ground).
        /// <paramref name="engineID"/> -1 gives it the template's engine; <paramref name="children"/> is its
        /// armaments and inventory in the container.child format (see <see cref="Loadouts"/>).
        /// </summary>
        public ItemNode CreateVehicle(int templateID, int engineID, string children = null)
        {
            lock (sync)
            {
                var template = ItemTemplates.Get(templateID);
                var vehicle = NewVehicle(new CharacterItem { ItemID = templateID, Name = template != null ? template.Name : null, Amount = engineID, Children = children });
                UnregisterTree(vehicle);
                return vehicle;
            }
        }

        private ItemNode NewVehicle(CharacterItem item)
        {
            var vehicle = BuildVehicle(item);
            RegisterTree(vehicle);
            return vehicle;
        }

        /// <summary>
        /// A vehicle with its armaments and inventory from its saved row, registered nowhere (the world's
        /// saved vehicles on the ground are built with it too). <paramref name="item"/>.Amount is the engine id
        /// (0 or -1: the template's); item.Children holds its cargo, armaments and health.
        /// </summary>
        public static ItemNode BuildVehicle(CharacterItem item)
        {
            var template = VehicleTemplates.Get(item.ItemID);
            int health = template != null ? template.Health : VehicleTemplates.DefaultHealth;
            int engine = item.Amount > 0 ? item.Amount : template != null ? template.EngineID : ItemTemplates.EngineOf(item.ItemID);

            var vehicle = new ItemNode(NewUniqueID(), ItemNode.Multi, item.ItemID)
            {
                Name = !string.IsNullOrEmpty(item.Name) ? item.Name : (template != null ? template.Name : null),
                EngineID = item.Amount > 0 ? item.Amount : -1,
            };
            vehicle.Created = UnixNow();
            vehicle.Health = health;
            vehicle.MaxHealth = health;
            vehicle.Options = new byte[6][];

            vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleArmaments) { Name = "armaments", Modified = -1, Created = -1 });
            var inventory = vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleInventory) { Name = "inventory", Modified = -1, Created = -1 });

            // Java's container.child format: "itemID-amount" separated by spaces; equipped armaments are
            // written "@slot-itemID"; ours adds "!health" for a damaged vehicle, and a worn or partly loaded
            // weapon or shield has its state after the entry ("@0-280000~d450~l30", see StateOf).
            var armaments = vehicle.Children.Find(c => c.StaticID == VehicleArmaments);
            foreach (var whole in (item.Children ?? string.Empty).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int first, second;
                int tilde = whole.IndexOf('~');
                string entry = tilde >= 0 ? whole.Substring(0, tilde) : whole;
                string state = tilde >= 0 ? whole.Substring(tilde) : null;
                if (entry.StartsWith("!"))
                {
                    if (int.TryParse(entry.Substring(1), out first) && first >= 0 && first < health)
                    {
                        vehicle.Health = first;
                    }
                    continue;
                }
                bool armament = entry.StartsWith("@");
                var parts = entry.TrimStart('@').Split('-');
                if (parts.Length != 2 || !int.TryParse(parts[0], out first) || !int.TryParse(parts[1], out second))
                {
                    continue;
                }
                if (armament && first >= 0 && first < MaxArmamentSlots && second > 0)
                {
                    var weapon = ApplyState(NewItem(second, 1, null), state);
                    if (VehicleEquipment.CanEquip(item.ItemID, first, second))
                    {
                        PadSlots(armaments, first);
                        armaments.SetSlot(first, weapon);
                    }
                    else
                    {
                        // Saved before the slot rules were checked: it goes in the cargo rather than being lost.
                        inventory.Add(weapon);
                    }
                }
                else if (!armament && first > 0)
                {
                    inventory.Add(ApplyState(NewItem(first, second > 0 ? second : 1, null), state));
                }
            }
            vehicle.Options[5] = VehicleStats(vehicle.Health, health, engine);
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
                entries.AddRange(inventory.Children.ConvertAll(c => c.StaticID + "-" + c.Amount + StateOf(c)));
            }
            var armaments = vehicle.Children.Find(c => c.StaticID == VehicleArmaments);
            if (armaments != null)
            {
                for (int i = 0; i < armaments.Children.Count; i++)
                {
                    if (!armaments.Children[i].IsEmptySlot)
                    {
                        entries.Add("@" + i + "-" + armaments.Children[i].StaticID + StateOf(armaments.Children[i]));
                    }
                }
            }
            if (vehicle.MaxHealth > 0 && vehicle.Health < vehicle.MaxHealth)
            {
                entries.Add("!" + Math.Max(0, vehicle.Health));
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

        /// <summary>
        /// What a saved item needs beyond its template and amount: "~d" + durability for a worn weapon or shield,
        /// "~l" + rounds for a weapon whose magazine is not as bought. Empty for everything else.
        /// </summary>
        public static string StateOf(ItemNode item)
        {
            if (!item.IsEquipment || item.Stats.Length < 2)
            {
                return string.Empty;
            }
            var state = string.Empty;
            if (item.Stats[0] != item.Stats[1])
            {
                state += "~d" + item.Stats[0];
            }
            var template = ItemTemplates.Get(item.StaticID);
            if (item.Loaded != (template != null && template.IsWeapon ? template.Magazine : 0))
            {
                state += "~l" + item.Loaded;
            }
            return state;
        }

        /// <summary>
        /// Puts back what <see cref="StateOf"/> saved; anything else in <paramref name="state"/> is ignored.
        /// </summary>
        public static ItemNode ApplyState(ItemNode item, string state)
        {
            if (string.IsNullOrEmpty(state) || !item.IsEquipment || item.Stats.Length < 2)
            {
                return item;
            }
            foreach (var token in state.Split(new[] { '~' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int value;
                if (token.Length < 2 || !int.TryParse(token.Substring(1), out value) || value < 0)
                {
                    continue;
                }
                if (token[0] == 'd')
                {
                    item.Stats[0] = Math.Min(value, item.Stats[1]);
                }
                else if (token[0] == 'l')
                {
                    item.Loaded = value;
                }
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

        /// <summary>
        /// The Space server numbers its items from 0x08400000, the Earth server from 0x00400000, so a shuttle
        /// keeps its unique id on the other side after a flight without meeting another item's.
        /// </summary>
        public static void SetUniqueIDBase(bool space)
        {
            nextUniqueID = space ? 0x08400000 : 0x00400000;
        }

        /// <summary>
        /// Gives the piloted vehicle the unique id it had on the other side's server (a shuttle after a flight:
        /// the client throws it away by that id).
        /// </summary>
        public void KeepPilotingID(uint uniqueID)
        {
            lock (sync)
            {
                if (Piloting == null || uniqueID == 0 || nodes.ContainsKey(uniqueID))
                {
                    return;
                }
                nodes.Remove(Piloting.UniqueID);
                Piloting.UniqueID = uniqueID;
                nodes[uniqueID] = Piloting;
            }
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
