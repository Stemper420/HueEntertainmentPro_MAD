using HueApi.Entertainment.Extensions;
using HueLightDJ.Services.ArtNet;
using HueLightDJ.Services.Interfaces;
using HueLightDJ.Services.Interfaces.Models;
using HueLightDJ.Services.Interfaces.Models.Requests;
using ProtoBuf.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueLightDJ.Services
{
  public class LightDJService : ILightDJService
  {
    private readonly EffectService effectService;
    private readonly StreamingSetup streamingSetup;
    private readonly ArtNetInputService artNetInputService;

    public LightDJService(EffectService effectService, StreamingSetup streamingSetup, ArtNetInputService artNetInputService)
    {
      this.effectService = effectService;
      this.streamingSetup = streamingSetup;
      this.artNetInputService = artNetInputService;
    }

    public async Task Connect(GroupConfiguration config, CallContext context = default)
    {
      //Connect
      await artNetInputService.StopAsync();
      effectService.StopEffects();
      try
      {
        await streamingSetup.SetupAndReturnGroupAsync(config);
        await artNetInputService.StartAsync(config);
      }
      catch
      {
        await artNetInputService.StopAsync();
        await streamingSetup.DisconnectAsync();
        throw;
      }
    }

    public Task<StatusModel> GetStatus(CallContext context = default)
    {
      StatusModel vm = new StatusModel();
      vm.Bpm = StreamingSetup.GetBPM();
      vm.IsAutoMode = EffectService.IsAutoModeRunning();
      vm.AutoModeHasRandomEffects = EffectService.AutoModeHasRandomEffects;
      vm.ShowDisconnect = !(StreamingSetup.CurrentConnection?.HideDisconnect ?? false);
      vm.CurrentGroup = StreamingSetup.CurrentConnection;
      vm.ArtNet = artNetInputService.GetStatus();

      if (StreamingSetup.CurrentConnection != null)
      {
        var groups = GroupService.GetAll();
        vm.Groups = groups.Select(x => new GroupInfoViewModel() { Name = x.Name }).ToList();
      }

      return Task.FromResult(vm);
    }

    public Task<IEnumerable<ArtNetBindAddress>> GetArtNetBindAddresses(CallContext context = default)
    {
      return Task.FromResult(artNetInputService.GetLocalBindAddresses());
    }

    public Task<EffectsVM> GetEffects(CallContext context = default)
    {
      return Task.FromResult(EffectService.GetEffectViewModels());
    }

    public Task StartEffect(StartEffectRequest request, CallContext context = default)
    {
      if (artNetInputService.IsExclusiveActive)
        return Task.CompletedTask;

      effectService.StartEffect(request.TypeName, request.ColorHex);
      return Task.CompletedTask;
    }

    public Task StartGroupEffect(StartEffectRequest request, CallContext context = default)
    {
      if (artNetInputService.IsExclusiveActive)
        return Task.CompletedTask;

      effectService.StartEffect(request.TypeName, request.ColorHex, request.GroupName, Enum.Parse<IteratorEffectMode>(request.IteratorMode!), Enum.Parse<IteratorEffectMode>(request.SecondaryIteratorMode!));
      return Task.CompletedTask;
    }

    public async Task IncreaseBPM(IntRequest req, CallContext context = default)
    {
      await streamingSetup.IncreaseBPM(req.Value);
      await GetStatus();
    }

    public async Task SetBPM(IntRequest req, CallContext context = default)
    {
      await streamingSetup.SetBPM(req.Value);
      await GetStatus();
    }

    public Task SetBri(DoubleRequest req, CallContext context = default)
    {
      var filterValue = 100 - req.Value;

      return streamingSetup.SetBrightnessFilter(filterValue);
    }


    public Task StartRandom(CallContext context = default)
    {
      if (artNetInputService.IsExclusiveActive)
        return Task.CompletedTask;

      effectService.StartRandomEffect();
      return Task.CompletedTask;
    }

    public Task StartAutoMode(CallContext context = default)
    {
      if (artNetInputService.IsExclusiveActive)
        return GetStatus();

      effectService.StartAutoMode();
      return GetStatus();
    }

    public Task StopAutoMode(CallContext context = default)
    {
      effectService.StopAutoMode();
      return Task.CompletedTask;
    }

    public Task SetAutoRandomMode(BoolRequest req, CallContext context = default)
    {
      EffectService.AutoModeHasRandomEffects = req.Value;
      return Task.CompletedTask;
    }

    [Obsolete]
    public Task ToggleAutoRandomMode(CallContext context = default)
    {
      EffectService.AutoModeHasRandomEffects = !EffectService.AutoModeHasRandomEffects;
      return Task.CompletedTask;
    }

    public Task StopEffects(CallContext context = default)
    {
      effectService.StopAutoMode();
      effectService.StopEffects();
      return Task.CompletedTask;
    }

    //public void SetColors(string[,] matrix)
    //{
    //  ManualControlService.SetColors(matrix);
    //}
    //public void SetColorsList(List<List<string>> matrix)
    //{
    //  ManualControlService.SetColors(matrix);
    //}
    public Task Beat(CallContext context = default)
    {
      if (artNetInputService.IsExclusiveActive)
        return Task.CompletedTask;

      effectService.Beat();
      return Task.CompletedTask;
    }

    public async Task Disconnect(CallContext context = default)
    {
      effectService.CancelAllEffects();
      await artNetInputService.StopAsync();

      await streamingSetup.DisconnectAsync();
    }


  }
}
