-- Character tables used by the Lobby-Server (character list and creation).
-- Same layout as the Java server's sql/characters.sql, appearance.sql, garments.sql,
-- char_skills.sql and container.sql (Java.zip), so an existing Java database can be reused.
-- container.child is used by the Java code but missing from its container.sql, so it is added here.
--
-- Ids: characters.char_id is the database id. appearance and garments use it as is; skills and
-- container use the id the client sees, which is char_id with a leading "1" (char_id 17 -> 117).
--
--   USE `titans-server`;

CREATE TABLE IF NOT EXISTS `characters` (
  `acc_id` int(10) NOT NULL,
  `char_id` int(10) NOT NULL AUTO_INCREMENT,
  `char_name` varchar(40) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `slot` int(1) DEFAULT NULL,
  `char_score` int(20) DEFAULT '0',
  `char_lost` int(20) DEFAULT '0',
  `char_money` int(20) DEFAULT '1000000',
  `char_access` int(2) DEFAULT '1',
  `zone` int(2) DEFAULT NULL,
  `x` int(20) DEFAULT '0',
  `y` int(20) DEFAULT '0',
  `z` int(20) DEFAULT '0',
  `rotx` int(20) DEFAULT '0',
  `roty` int(20) DEFAULT '0',
  `direction` int(20) DEFAULT '0',
  `date_created` longtext COLLATE utf8_bin,
  `team_id` int(11) NOT NULL DEFAULT '-1',
  PRIMARY KEY (`char_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

CREATE TABLE IF NOT EXISTS `appearance` (
  `char_id` int(10) NOT NULL default '0',
  `face` int(1) NOT NULL default '0',
  `faction` int(1) NOT NULL default '1',
  `gender` int(1) NOT NULL default '1',
  `skin` int(1) NOT NULL default '1',
  `hairstyle` int(1) NOT NULL default '1',
  `haircolor` int(1) NOT NULL default '1',
  `rank` int(1) NOT NULL default '1'
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

-- Each column holds "item id,style"; -1,0 is nothing worn.
CREATE TABLE IF NOT EXISTS `garments` (
  `char_id` TINYTEXT NOT NULL,
  `hat` TINYTEXT NOT NULL,
  `glasses` TINYTEXT NOT NULL,
  `coat` TINYTEXT NOT NULL,
  `top` TINYTEXT NOT NULL,
  `bottom` TINYTEXT NOT NULL,
  `gloves` TINYTEXT NOT NULL,
  `dress` TINYTEXT NOT NULL,
  `shoes` TINYTEXT NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

-- skill_idx is the index in Common.Characters.Skill (the Java SkillSet order).
CREATE TABLE IF NOT EXISTS `skills` (
  `char_id` int(10) NOT NULL default '0',
  `skill_idx` int(10) NOT NULL default '0',
  `skill_level` int(10) NOT NULL default '0',
  `skill_exp` int(10) NOT NULL default '0'
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

-- item_amount is the stack size, or for a vehicle its engine id (-1 = the template's engine).
CREATE TABLE IF NOT EXISTS `container` (
  `char_id` int(10) NOT NULL default '0',
  `container_id` int(10) NOT NULL default '0',
  `container_name` TINYTEXT,
  `item_id` int(10) NOT NULL default '-1',
  `item_name` TINYTEXT,
  `item_amount` INT(10) NOT NULL default '0',
  `child` TEXT
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;
