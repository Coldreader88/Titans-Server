-- GM teleport bookmarks for the CMS server's #bookmark command, from the Java server
-- (Java.zip sql/tele_bookmark.sql). zone 1 is Earth, 2 is space; #bookmark does not check the zone.
--
--   USE `titans-server`;

CREATE TABLE IF NOT EXISTS `tele_bookmark` (
  `id` tinyint(20) NOT NULL AUTO_INCREMENT,
  `name` varchar(40) COLLATE utf8_bin DEFAULT NULL,
  `x` int(20) DEFAULT NULL,
  `y` int(20) DEFAULT NULL,
  `z` int(20) DEFAULT NULL,
  `direction` int(20) DEFAULT NULL,
  `rotx` int(20) DEFAULT NULL,
  `roty` int(20) DEFAULT NULL,
  `zone` int(20) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

INSERT IGNORE INTO tele_bookmark VALUES ('1', 'Sydney', '72536544', '-59308692', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('2', 'Perth', '55469808', '-58348644', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('3', 'Canberra', '71572992', '-60098004', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('4', 'Adelaide', '66390864', '-59786388', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('5', 'Melbourne', '69405264', '-61194804', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('6', 'Darwin', '62637696', '-49079604', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('7', 'Brisbane', '73447632', '-56285652', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('8', 'Southern Cross', '57289968', '-58213572', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('9', 'Newman', '56912259', '-54484061', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('10', 'Richmond', '68951894', '-52883935', '200', '0', null, null, '1');
INSERT IGNORE INTO tele_bookmark VALUES ('11', 'Isaeo 29', '-3424793', '5686960', '-421', '5306', '-2022', '-6446', '2');
INSERT IGNORE INTO tele_bookmark VALUES ('12', 'Isaeo 28Z', '6267610', '-815707', '503', '-18219', '-8150', '-3296', '2');
INSERT IGNORE INTO tele_bookmark VALUES ('13', 'Tasmania EF', '70036683', '-63257419', '3542', '0', '0', '0', '1');
INSERT IGNORE INTO tele_bookmark VALUES ('14', 'Tasmania Zeon', '69616694', '-63019734', '975', '0', '0', '0', '1');
INSERT IGNORE INTO tele_bookmark VALUES ('15', 'Brisbane Supply Team 5', '73308385', '-56086507', '1678', '0', '0', '0', '1');
