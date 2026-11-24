' RunHidden.vbs — lance server.ps1 sans aucune fenêtre visible (pas de console PowerShell sur le bureau).
' Utilisation : place un RACCOURCI vers ce fichier (pas le .vbs lui-même) dans le dossier Démarrage de Windows
' (touche Windows + R, taper shell:startup, Entrée), pour que le serveur démarre tout seul, en arrière-plan,
' à l'ouverture de la session.
'
' Ce fichier doit rester à côté de server.ps1 (il se sert de son propre dossier pour le retrouver).

Dim shell, fso, scriptDir, serverPath, command

Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
serverPath = fso.BuildPath(scriptDir, "server.ps1")

If Not fso.FileExists(serverPath) Then
    MsgBox "server.ps1 est introuvable à côté de RunHidden.vbs (" & scriptDir & ")." & vbCrLf & _
           "Vérifie que RunHidden.vbs n'a pas été déplacé hors du dossier du serveur.", vbExclamation, "Stream Deck"
    WScript.Quit 1
End If

command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & serverPath & """"

Set shell = CreateObject("WScript.Shell")
' Le 0 = fenêtre cachée ; le False = ne pas attendre la fin (le serveur tourne en continu).
shell.Run command, 0, False
