SET INSTALL_DIR=.\DummyDns
cmd /k java -cp "%INSTALL_DIR%";"%INSTALL_DIR%\dummydns.jar" ucgo.dns.DNSServer
exit /b