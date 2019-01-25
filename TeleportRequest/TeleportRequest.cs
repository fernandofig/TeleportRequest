﻿using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace TeleportRequest
{
	[ApiVersion(2, 1)]
	public class TeleportRequest : TerrariaPlugin
	{
		public override string Author
		{
			get { return "MarioE, maintained by Ryozuki"; }
		}
		public Config Config = new Config();
		public override string Description
		{
			get { return "Adds teleportation accept commands."; }
		}
		public override string Name
		{
			get { return "Teleport"; }
		}
		private Timer Timer;
		private bool[] TPAllows = new bool[256];
		private TPRequest[] TPRequests = new TPRequest[256];
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public TeleportRequest(Main game)
			: base(game)
		{
			for (int i = 0; i < TPRequests.Length; i++)
				TPRequests[i] = new TPRequest();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				Timer.Dispose();
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		void OnElapsed(object sender, ElapsedEventArgs e)
		{
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
				if (tpr.timeout > 0)
				{
					TSPlayer dst = TShock.Players[tpr.dst];
					TSPlayer src = TShock.Players[i];

					tpr.timeout--;
					if (tpr.timeout == 0)
					{
						src.SendErrorMessage("Your teleport request timed out.");
						dst.SendInfoMessage("{0}'s teleport request timed out.", src.Name);
					}
					else
					{
						string msg = String.Format("{{0}} is requesting to teleport to you. ({0}tpok or {0}tpdeny)", Commands.Specifier);
						if (tpr.dir)
							msg = String.Format("You are requested to teleport to {{0}}. ({0}tpok or {0}tpdeny)", Commands.Specifier);
						dst.SendInfoMessage(msg, src.Name);
					}
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			Commands.ChatCommands.Add(new Command("tprequest.ok", TPAccept, "tpok")
			{
				AllowServer = false,
				HelpText = "Accepts a teleport request."
			});
			Commands.ChatCommands.Add(new Command("tprequest.autodeny", TPAutoDeny, "tpautodeny")
			{
				AllowServer = false,
				HelpText = "Toggles automatic denial of teleport requests."
			});
			Commands.ChatCommands.Add(new Command("tprequest.deny", TPDeny, "tpdeny")
			{
				AllowServer = false,
				HelpText = "Denies a teleport request."
			});
			Commands.ChatCommands.Add(new Command("tprequest.tpahere", TPAHere, "tpahere")
			{
				AllowServer = false,
				HelpText = "Sends a request for someone to teleport to you."
			});
			Commands.ChatCommands.Add(new Command("tprequest.tpa", TPA, "tpa")
			{
				AllowServer = false,
				HelpText = "Sends a request to teleport to someone."
			});

			if (File.Exists(Path.Combine(TShock.SavePath, "tpconfig.json")))
				Config = Config.Read(Path.Combine(TShock.SavePath, "tpconfig.json"));
			Config.Write(Path.Combine(TShock.SavePath, "tpconfig.json"));
			Timer = new Timer(Config.Interval * 1000);
			Timer.Elapsed += OnElapsed;
			Timer.Start();
		}
		void OnLeave(LeaveEventArgs e)
		{
			TPAllows[e.Who] = false;
			TPRequests[e.Who].timeout = 0;
		}

		void TPA(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tpa <player>", Commands.Specifier);
				return;
			}

			string plrName = String.Join(" ", e.Parameters.ToArray());
			var players = TSPlayer.FindByNameOrID(plrName);
			if (players.Count == 0)
				e.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				e.Player.SendErrorMessage("More than one player matched!");
			else if ((!players[0].TPAllow || TPAllows[players[0].Index]) && !e.Player.Group.HasPermission(Permissions.tpoverride))
				e.Player.SendErrorMessage("You cannot teleport to {0}.", players[0].Name);
			else
			{
				for (int i = 0; i < TPRequests.Length; i++)
				{
					TPRequest tpr = TPRequests[i];
					if (tpr.timeout > 0 && tpr.dst == players[0].Index)
					{
						e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
						return;
					}
				}
				TPRequests[e.Player.Index].dir = false;
				TPRequests[e.Player.Index].dst = (byte)players[0].Index;
				TPRequests[e.Player.Index].timeout = Config.Timeout + 1;
				e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
			}
		}
		void TPAccept(CommandArgs e)
		{
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
                if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
				{
					TSPlayer plr1 = tpr.dir ? e.Player : TShock.Players[i];
					TSPlayer plr2 = tpr.dir ? TShock.Players[i] : e.Player;
                    if (plr1.Teleport(plr2.X, plr2.Y))
					{
						plr1.SendSuccessMessage("Teleported to {0}.", plr2.Name);
						plr2.SendSuccessMessage("{0} teleported to you.", plr1.Name);
					}
					tpr.timeout = 0;
                    return;
				}
			}
			e.Player.SendErrorMessage("You have no pending teleport requests.");
		}
		void TPAHere(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}tpahere <player>", Commands.Specifier);
				return;
			}

			string plrName = String.Join(" ", e.Parameters.ToArray());
			var players = TSPlayer.FindByNameOrID(plrName);
			if (players.Count == 0)
				e.Player.SendErrorMessage("Invalid player!");
			else if (players.Count > 1)
				e.Player.SendErrorMessage("More than one player matched!");
			else if ((!players[0].TPAllow || TPAllows[players[0].Index]) && !e.Player.Group.HasPermission(Permissions.tpoverride))
				e.Player.SendErrorMessage("You cannot teleport {0}.", players[0].Name);
			else
			{
				for (int i = 0; i < TPRequests.Length; i++)
				{
					TPRequest tpr = TPRequests[i];
					if (tpr.timeout > 0 && tpr.dst == players[0].Index)
					{
						e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
						return;
					}
				}
				TPRequests[e.Player.Index].dir = true;
				TPRequests[e.Player.Index].dst = (byte)players[0].Index;
				TPRequests[e.Player.Index].timeout = Config.Timeout + 1;
				e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
			}
		}
		void TPAutoDeny(CommandArgs e)
		{
			TPAllows[e.Player.Index] = !TPAllows[e.Player.Index];
			e.Player.SendInfoMessage("{0}abled Teleport Auto-deny.", TPAllows[e.Player.Index] ? "En" : "Dis");
		}
		void TPDeny(CommandArgs e)
		{
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
				if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
				{
					e.Player.SendSuccessMessage("Denied {0}'s teleport request.", TShock.Players[i].Name);
					TShock.Players[i].SendErrorMessage("{0} denied your teleport request.", e.Player.Name);
					return;
				}
			}
			e.Player.SendErrorMessage("You have no pending teleport requests.");
		}
	}
}