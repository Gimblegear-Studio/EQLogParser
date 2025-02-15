﻿using System;
using System.Collections.Generic;
using System.Globalization;

namespace EQLogParser
{
  class ChatLineParser
  {
    private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    private static readonly DateUtil DateUtil = new DateUtil();

    private static readonly List<string> YouCriteria = new List<string>
    {
      "You say,", "You told ", "You tell ", "You say to ", "You shout,", "You say out of", "You auction,"
    };

    private static readonly List<string> OtherCriteria = new List<string>
    {
      " says,", " tells ", " shouts,", " says out of", " auctions,", " told you,"
    };

    internal static ChatType Process(LineData lineData)
    {
      var chatType = ParseChatType(lineData.Line);
      if (chatType != null && chatType.SenderIsYou == false && chatType.Sender != null)
      {
        if (chatType.Channel == ChatChannels.Guild || chatType.Channel == ChatChannels.Raid || chatType.Channel == ChatChannels.Fellowship)
        {
          PlayerManager.Instance.AddVerifiedPlayer(chatType.Sender, DateUtil.ParseLogDate(lineData.Line, out _));
        }
      }

      return chatType;
    }

    internal static ChatType ParseChatType(string line)
    {
      ChatType chatType = null;

      if (!string.IsNullOrEmpty(line) && line.Length > (LineParsing.ActionIndex + 3))
      {
        try
        {
          int count;
          int max = Math.Min(16, line.Length - LineParsing.ActionIndex);
          int index = YouCriteria.FindIndex(criteria => line.IndexOf(criteria, LineParsing.ActionIndex, max, StringComparison.Ordinal) > -1);

          if (index < 0)
          {
            int criteriaIndex = -1;
            for (int i = 0; i < OtherCriteria.Count; i++)
            {
              int lastIndex = line.IndexOf("'", LineParsing.ActionIndex, StringComparison.Ordinal);
              if (lastIndex > -1)
              {
                count = lastIndex - LineParsing.ActionIndex;
                if (count > 0 && line.Length >= (LineParsing.ActionIndex + count))
                {
                  criteriaIndex = line.IndexOf(OtherCriteria[i], LineParsing.ActionIndex, count, StringComparison.Ordinal);
                  if (criteriaIndex > -1)
                  {
                    index = i;
                    break;
                  }
                }
              }
            }

            if (index < 0)
            {
              index = line.IndexOf(" ", LineParsing.ActionIndex, StringComparison.Ordinal);
              if (index > -1 && index + 5 < line.Length)
              {
                if (line[index + 1] == '-' && line[index + 2] == '>' && line.Length >= (index + 4))
                {
                  int lastIndex = line.IndexOf(":", index + 4, StringComparison.Ordinal);
                  if (lastIndex > -1)
                  {
                    string sender = line.Substring(LineParsing.ActionIndex, index - LineParsing.ActionIndex);
                    string receiver = line.Substring(index + 4, lastIndex - index - 4);
                    chatType = new ChatType { Channel = ChatChannels.Tell, Sender = sender, Receiver = receiver, Line = line, AfterSenderIndex = lastIndex };

                    if (ConfigUtil.PlayerName == sender)
                    {
                      chatType.SenderIsYou = true;
                    }
                  }
                }
              }
            }
            else if (index > -1 && criteriaIndex > -1)
            {
              int start, end;
              int senderLen = criteriaIndex - LineParsing.ActionIndex;
              chatType = new ChatType { SenderIsYou = false, Sender = line.Substring(LineParsing.ActionIndex, senderLen), AfterSenderIndex = criteriaIndex, Line = line };

              switch (index)
              {
                case 0:
                  chatType.Channel = ChatChannels.Say;
                  break;
                case 1:
                  start = criteriaIndex + 7;
                  if (line.Length >= (start + 5) && line.IndexOf("you, ", start, 5, StringComparison.Ordinal) > -1)
                  {
                    chatType.Channel = ChatChannels.Tell;
                    chatType.Receiver = "You";
                  }
                  else if (line.Length >= (start + 9) && line.IndexOf("the guild", start, 9, StringComparison.Ordinal) > -1)
                  {
                    chatType.Channel = ChatChannels.Guild;
                  }
                  else if (line.Length >= (start + 9) && line.IndexOf("the group", start, 9, StringComparison.Ordinal) > -1)
                  {
                    chatType.Channel = ChatChannels.Group;
                  }
                  else if (line.Length >= (start + 8) && line.IndexOf("the raid", start, 8, StringComparison.Ordinal) > -1)
                  {
                    chatType.Channel = ChatChannels.Raid;
                  }
                  else if (line.Length >= (start + 14) && line.IndexOf("the fellowship", start, 14, StringComparison.Ordinal) > -1)
                  {
                    chatType.Channel = ChatChannels.Fellowship;
                  }
                  else if ((end = line.IndexOf(":", start + 1, StringComparison.Ordinal)) > -1)
                  {
                    chatType.Channel = line.Substring(start, end - start);
                    chatType.Channel = char.ToUpper(chatType.Channel[0], CultureInfo.CurrentCulture) + chatType.Channel.Substring(1).ToLower(CultureInfo.CurrentCulture);
                  }
                  break;
                case 2:
                  chatType.Channel = ChatChannels.Shout;
                  break;
                case 3:
                  chatType.Channel = ChatChannels.Ooc;
                  break;
                case 4:
                  chatType.Channel = ChatChannels.Auction;
                  break;
                case 5:
                  // check if it's an old cross server tell and not an NPC
                  if (line.Length >= (criteriaIndex + 10) && line.IndexOf(" told you,", criteriaIndex, 10, StringComparison.Ordinal) > -1 && chatType.Sender.IndexOf(".", StringComparison.Ordinal) > -1)
                  {
                    chatType.Channel = ChatChannels.Tell;
                    chatType.Receiver = "You";
                  }
                  break;
              }
            }
          }
          else
          {
            int start, end;
            chatType = new ChatType { SenderIsYou = true, Sender = "You", AfterSenderIndex = LineParsing.ActionIndex + 4, Line = line };
            switch (index)
            {
              case 0:
                chatType.Channel = ChatChannels.Say;
                break;
              case 1:
                chatType.Channel = ChatChannels.Tell;

                start = LineParsing.ActionIndex + 9;
                if ((end = line.IndexOf(",", start, StringComparison.Ordinal)) > -1)
                {
                  chatType.Receiver = line.Substring(start, end - start);
                }
                break;
              case 2:
                start = LineParsing.ActionIndex + 9;

                if (line.Length >= (start + 10) && line.IndexOf("your party", start, 10, StringComparison.Ordinal) > -1)
                {
                  chatType.Channel = ChatChannels.Group;
                }
                else if (line.Length >= (start + 9) && line.IndexOf("your raid", start, 9, StringComparison.Ordinal) > -1)
                {
                  chatType.Channel = ChatChannels.Raid;
                }
                else
                {
                  if ((end = line.IndexOf(":", start, StringComparison.Ordinal)) > -1)
                  {
                    chatType.Channel = line.Substring(start, end - start);
                    chatType.Channel = char.ToUpper(chatType.Channel[0], CultureInfo.CurrentCulture) + chatType.Channel.Substring(1).ToLower(CultureInfo.CurrentCulture);
                  }
                }
                break;
              case 3:
                start = LineParsing.ActionIndex + 11;
                if (line.Length >= (start + 10) && line.IndexOf("your guild", start, 10, StringComparison.Ordinal) > -1)
                {
                  chatType.Channel = ChatChannels.Guild;
                }
                else if (line.Length >= (start + 15) && line.IndexOf("your fellowship", start, 15, StringComparison.Ordinal) > -1)
                {
                  chatType.Channel = ChatChannels.Fellowship;
                }
                break;
              case 4:
                chatType.Channel = ChatChannels.Shout;
                break;
              case 5:
                chatType.Channel = ChatChannels.Ooc;
                break;
              case 6:
                chatType.Channel = ChatChannels.Auction;
                break;
            }
          }
        }
        catch (Exception ex)
        {
          LOG.Error("Failed parsing line = " + line);
          LOG.Error(ex);
        }
      }

      return chatType;
    }
  }
  internal static class ChatChannels
  {
    public const string Auction = "Auction";
    public const string Say = "Say";
    public const string Guild = "Guild";
    public const string Fellowship = "Fellowship";
    public const string Tell = "Tell";
    public const string Shout = "Shout";
    public const string Group = "Group";
    public const string Raid = "Raid";
    public const string Ooc = "OOC";
  }
}
