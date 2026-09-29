-- What the game servers keep besides the characters. Each game server creates these tables itself when
-- they are missing, so importing this file is optional.
--
--   USE `titans-server`;

-- Everything lying on the ground (items, vehicles, wrecks and their cargo), per zone (1 Earth, 2 Space).
-- The game server writes its zone's rows while the ground changes and reads them back at startup.
-- child: the vehicle's cargo and armaments as in container.child ("itemID-amount", "@slot-itemID",
-- "!health" when damaged); item_amount: a vehicle's engine id.
CREATE TABLE IF NOT EXISTS ground_items (
  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  zone SMALLINT NOT NULL,
  item_id INT NOT NULL,
  item_name VARCHAR(64) NOT NULL DEFAULT '',
  item_amount INT NOT NULL DEFAULT 1,
  child TEXT,
  vehicle TINYINT NOT NULL DEFAULT 0,
  wreck TINYINT NOT NULL DEFAULT 0,
  x INT NOT NULL, y INT NOT NULL, z INT NOT NULL,
  rotation VARCHAR(12) NOT NULL DEFAULT '000000000000',
  owner_id INT UNSIGNED NOT NULL,
  placed INT NOT NULL,
  KEY zone (zone)
);

-- Shuttle flights between the Earth and Space game servers: written by the server the player takes off
-- from (0x40), read and removed by the other one when the client asks for the player info (0x5F).
CREATE TABLE IF NOT EXISTS flights (
  char_id INT UNSIGNED NOT NULL PRIMARY KEY,
  cluster SMALLINT NOT NULL,
  vehicle_uid INT UNSIGNED NOT NULL DEFAULT 0,
  transport_a INT NOT NULL, transport_b INT NOT NULL,
  x INT NOT NULL, y INT NOT NULL, z INT NOT NULL
);

-- The battle towns Richmond (58) and Newman (59), kept by the Earth Game server (World/Occupation.cs creates it
-- when missing). owner, attacker: 1 Federation, 2 Zeon. status: 0 peace, 1 open to attack, 2 war. time: Unix
-- time the attack window opens (peace) or the war ends. icf: owner of each of the five ICFs, comma separated.
CREATE TABLE IF NOT EXISTS `occupation_city` (
  `city_id` int NOT NULL,
  `owner` smallint NOT NULL,
  `status` int NOT NULL,
  `time` int NOT NULL,
  `icf` varchar(32) NOT NULL,
  `attacker` smallint NOT NULL default 0,
  `started_by` int unsigned NOT NULL default 0,
  PRIMARY KEY (`city_id`)
);
