-- Accounts table used by the Lobby-Server login.
-- Same layout as the Java server's sql/MySQL Account.sql (Java.zip), so an existing
-- Java database can be reused. Passwords are salted PBKDF2 hashes ("pbkdf2$..."); the Java server's
-- unsalted SHA-1 hashes still log in and are replaced at the next login.
--
--   CREATE DATABASE IF NOT EXISTS `titans-server`;
--   USE `titans-server`;

CREATE TABLE IF NOT EXISTS `accounts` (
  `acc_id` int(10) NOT NULL AUTO_INCREMENT,
  `name` varchar(25) COLLATE utf8_unicode_ci DEFAULT NULL,
  `password` varchar(128) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
  `acc_level` int(10) DEFAULT NULL,
  `email` varchar(50) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
  `status` int(10) DEFAULT '1',
  `ban_time` date DEFAULT NULL,
  `f_name` varchar(45) COLLATE utf8_unicode_ci DEFAULT NULL,
  `l_name` varchar(45) COLLATE utf8_unicode_ci DEFAULT NULL,
  `sex` varchar(2) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
  `dob` varchar(45) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
  `country` varchar(5) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
  `newsletter` varchar(5) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT 'Y',
  `creation_date` varchar(45) CHARACTER SET utf8 COLLATE utf8_bin NOT NULL,
  `last_ip` varchar(45) COLLATE utf8_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`acc_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- Session key from the login reply (0x38000), with the character taken to the game server (0x30005).
-- The Game server checks it when the client logs in (0x41), and the game and chat servers only take the
-- character from the address (ip) the player logged in to the Lobby from. The Lobby server creates this
-- table (and the ip column) itself when it is missing.
CREATE TABLE IF NOT EXISTS `login_sessions` (
  `acc_id` int(10) NOT NULL,
  `session_key` int(10) unsigned NOT NULL,
  `char_id` int(10) unsigned NOT NULL default '0',
  `updated` int(10) NOT NULL default '0',
  `ip` varchar(45) NOT NULL default '',
  PRIMARY KEY (`acc_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;
