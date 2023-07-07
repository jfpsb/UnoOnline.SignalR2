using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using UnoOnline.Model;

namespace UnoOnline.SignalR2.Hubs
{
    public class UnoOnlineHub : Hub
    {
        private static Dictionary<Guid, Carta> baralho = new Dictionary<Guid, Carta>();
        private static StatusPartida? statusPartida;

        public UnoOnlineHub()
        {
            
        }

        public override Task OnConnectedAsync()
        {
            if (statusPartida == null)
            {
                statusPartida = new StatusPartida();
                GeraBaralho();
            }
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine("GONE");
            return base.OnDisconnectedAsync(exception);
        }

        private void GeraBaralho()
        {
            var ccs = JsonConvert.DeserializeObject<List<Carta>>(File.ReadAllText("Resources/baralho-uno.json"));
            foreach (var c in ccs)
            {
                for (int i = 0; i < c.Quantidade; i++)
                {
                    c.Uuid = Guid.NewGuid();

                    string[] tipoArray = c.Codigo.Split('-');

                    switch (tipoArray[0])
                    {
                        case "amarelo":
                            c.Tipo = "numeral";
                            c.Cor = "amarelo";
                            break;
                        case "verde":
                            c.Tipo = "numeral";
                            c.Cor = "verde";
                            break;
                        case "azul":
                            c.Tipo = "numeral";
                            c.Cor = "azul";
                            break;
                        case "vermelho":
                            c.Tipo = "numeral";
                            c.Cor = "vermelho";
                            break;
                        case "inverter":
                            c.Tipo = tipoArray[0];
                            c.Cor = tipoArray[1];
                            break;
                        default:
                            c.Tipo = tipoArray[0];
                            break;
                    }

                    baralho.Add(c.Uuid, c);
                }
            }
        }
        private IList<Carta> RetornaCartasDoBaralho(int quantCartas)
        {
            List<Carta> cartasSelecionadas = new List<Carta>();
            List<Carta> cartasNoBaralho = baralho.Where(w => w.Value.EstaEmBaralho).Select(s => s.Value).ToList();

            for (int i = 0; i < quantCartas; i++)
            {
                var carta = cartasNoBaralho[new Random().Next(cartasNoBaralho.Count)];
                cartasSelecionadas.Add(carta);
            }

            return cartasSelecionadas;
        }

        public async Task EnviaStatusPartida(StatusPartida statusPartida)
        {
            await Clients.All.SendAsync("AtualizarStatusPartida", statusPartida);
        }
        public async Task EntrarEmPartida(Jogador jogador)
        {
            if (statusPartida.Jogadores.Count >= 4)
            {
                await Clients.Caller.SendAsync("MensagemMessageBox", "A sala do jogo está cheia!");
                return;
            }

            statusPartida.AdicionaJogador(jogador);
            await Clients.All.SendAsync("AtualizarStatusPartida", statusPartida);
        }
        //public async Task MensagemTeste(string txt)
        //{
        //    Console.WriteLine(txt);
        //    await Clients.All.SendAsync("RecebeMensagemTeste", txt);
        //}
    }
}
