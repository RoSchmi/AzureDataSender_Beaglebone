@echo off
REM
REM Projekt: Open Connection to BeagleBone  -- Change IP-Address to your needs
net use Q: \\192.168.1.41\debian /user:debian *
REM net use * \\beaglebone\debian /user:debian
REM net use * \\beaglebone\debian /user:debian B22XXXXXX
REM net use * /delete
REM C:\>net use * \\beaglebone\debian /u:debian *
Pause
@echo on