-- Tables used by the CMS server (chat, friends, teams). The CMS server creates them when they are
-- missing, so running this by hand is optional.
--
-- team is the Java server's table (Java.zip sql/team.sql) plus a created column (Unix seconds).
-- friends is new: the Java server kept friend lists in memory only.
-- Character ids in both are the ids the client sees: characters.char_id with a leading "1"
-- (char_id 17 -> 117), the same as characters.team_id holds team.id.
-- Player team ids start at 51500000 (the Java id chain digit 5, then a counter from 1500000).
--
-- The CMS server also reads teams (NPC squad names) and tele_bookmark (GM #bookmark) from the Java
-- server's sql/ folder when they exist.
--
--   USE `titans-server`;

CREATE TABLE IF NOT EXISTS `team` (
  `creatorName` text COLLATE utf8_bin NOT NULL,
  `creatorID` int(11) NOT NULL,
  `name` text CHARACTER SET utf8 NOT NULL,
  `id` int(11) NOT NULL,
  `created` int(11) NOT NULL DEFAULT '0'
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

-- For a team table made from the Java team.sql (MariaDB syntax):
--   ALTER TABLE `team` ADD COLUMN IF NOT EXISTS `created` int(11) NOT NULL DEFAULT '0';

-- When each character last created a team (CMSServer.xml TeamRecreateDays: no new team for 7 days).
CREATE TABLE IF NOT EXISTS `team_created` (
  `char_id` int(10) unsigned NOT NULL PRIMARY KEY,
  `created` int(11) NOT NULL DEFAULT '0'
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `friends` (
  `char_id` int(10) unsigned NOT NULL,
  `friend_id` int(10) unsigned NOT NULL,
  PRIMARY KEY (`char_id`, `friend_id`),
  KEY `friend_id` (`friend_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;
