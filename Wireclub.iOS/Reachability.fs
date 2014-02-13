module Wireclub.iOS.Reachability

open System
open System.Net
open MonoTouch.Foundation
open MonoTouch.UIKit
open MonoTouch.SystemConfiguration
open MonoTouch.CoreFoundation

type NetworkStatus =
| NotReachable = 0
| ReachableViaCarrierDataNetwork = 1
| ReachableViaWiFiNetwork = 2


let HostName = "www.google.com";

let isReachableWithoutRequiringConnection (flags:NetworkReachabilityFlags) =

    // Is it reachable with the current network configuration?
    let isReachable = (int flags &&& int NetworkReachabilityFlags.Reachable) <> 0

    // Do we need a connection to reach it?
    let mutable noConnectionRequired = (int flags &&& int NetworkReachabilityFlags.ConnectionRequired) = 0

    // Since the network stack will automatically try to get the WAN up,
    // probe that
    if ((int flags &&& int NetworkReachabilityFlags.IsWWAN) <> 0) then
        noConnectionRequired <- true

    isReachable && noConnectionRequired


// Is the host reachable with the current network configuration
let isHostReachable host =
    if (String.IsNullOrEmpty host) then
        false
    else
        use r = new NetworkReachability (host)
        match r.TryGetFlags () with
        | true, flags -> isReachableWithoutRequiringConnection flags
        | _ -> false

let onReachabilityChanged = new Event<NetworkReachabilityFlags> ()

let checkReachability (reachability: NetworkReachability) =
    reachability.SetCallback (fun flags -> onReachabilityChanged.Trigger flags) |> ignore
    reachability.Schedule (CFRunLoop.Current, CFRunLoop.ModeDefault) |> ignore
    reachability

let isAvailable (reachability:NetworkReachability) =
    match reachability.TryGetFlags () with
    | true, flags -> isReachableWithoutRequiringConnection flags, flags
    | _, flags -> false, flags

let adHocWiFiNetworkReachability = checkReachability (new NetworkReachability(new IPAddress ([| 169uy; 254uy; 0uy; 0uy |])))
let isAdHocWiFiNetworkAvailable () = isAvailable adHocWiFiNetworkReachability
  
let defaultRouteReachability = checkReachability (new NetworkReachability(new IPAddress (0L)))
let isNetworkAvailable () = isAvailable defaultRouteReachability

let remoteHostReachability =
    let reachability = new NetworkReachability (HostName)
    // Need to probe before we queue, or we wont get any meaningful values
    // this only happens when you create NetworkReachability from a hostname
    reachability.TryGetFlags () |> ignore
    checkReachability reachability

let remoteHostStatus () =
    match remoteHostReachability.TryGetFlags () with
    | true, flags when isReachableWithoutRequiringConnection flags = false -> NetworkStatus.NotReachable
    | true, flags when int (flags &&& NetworkReachabilityFlags.IsWWAN) <> 0 -> NetworkStatus.ReachableViaCarrierDataNetwork
    | true, _ -> NetworkStatus.ReachableViaWiFiNetwork
    | _ -> NetworkStatus.NotReachable

let internetConnectionStatus () =
    match isNetworkAvailable () with
    | true, flags when int (flags &&& NetworkReachabilityFlags.IsDirect) <> 0 -> NetworkStatus.NotReachable
    | _, flags when int (flags &&& NetworkReachabilityFlags.IsWWAN) <> 0 -> NetworkStatus.ReachableViaCarrierDataNetwork
    | _, flags when int flags = 0 -> NetworkStatus.NotReachable
    | _ -> NetworkStatus.ReachableViaWiFiNetwork

let localWifiConnectionStatus () =
    match isAdHocWiFiNetworkAvailable () with
    | true, flags when int (flags &&& NetworkReachabilityFlags.IsDirect) <> 0 -> NetworkStatus.ReachableViaWiFiNetwork
    | _ -> NetworkStatus.NotReachable

