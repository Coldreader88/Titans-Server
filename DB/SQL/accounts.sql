-- Accounts table used by the Lobby-Server login.
-- Same layout as the Java server's sql/MySQL Account.sql (Java.zip), so an existing
-- Java database can be reused. Passwords are lowercase hex SHA-1.
--
--   CREATE DATABASE IF NOT EXISTS `titans-server`;
--   USE `titans-server`;

CREATE TABLE IF NOT EXISTS `accounts` (
  `acc_id` int(10) NOT NULL AUTO_INCREMENT,
  `name` varchar(25) COLLATE utf8_unicode_ci DEFAULT NULL,
  `password` varchar(45) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
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
