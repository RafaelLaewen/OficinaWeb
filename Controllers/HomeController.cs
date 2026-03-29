using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace OficinaWeb.Controllers;

public class HomeController : Controller
{
    private string connectionString = "Host=localhost;Username=postgres;Password=XXXXXXXXXXXX;Database=sistema_oficina";

    public IActionResult Index() => View();
    public IActionResult CadastrarCliente() => View();

    [HttpPost]
    public IActionResult SalvarCliente(string nome, string cpf, string telefone, string placa, string marca, string modelo, int? ano)
    {
        if (!string.IsNullOrEmpty(cpf)) cpf = cpf.Replace(".", "").Replace("-", "").Replace(" ", "");
        if (!string.IsNullOrEmpty(placa)) placa = placa.Replace("-", "").Replace(" ", "").ToUpper();
        if (!string.IsNullOrEmpty(telefone)) telefone = telefone.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "");

        try 
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                string sqlCheck = "SELECT COUNT(*) FROM gestao.clientes WHERE cpf = @c";
                using (var cmdCheck = new NpgsqlCommand(sqlCheck, conn))
                {
                    cmdCheck.Parameters.AddWithValue("c", cpf);
                    long existe = (long)cmdCheck.ExecuteScalar();
                    if (existe > 0) return Content("Atenção: O CPF " + cpf + " já está cadastrado!");
                }
                
                string sqlC = "INSERT INTO gestao.clientes (nome, cpf, telefone) VALUES (@n, @c, @t) RETURNING id_cliente";
                using (var cmd = new NpgsqlCommand(sqlC, conn))
                {
                    cmd.Parameters.AddWithValue("n", nome);
                    cmd.Parameters.AddWithValue("c", cpf);
                    cmd.Parameters.AddWithValue("t", (object)telefone ?? DBNull.Value);
                    int idCliente = Convert.ToInt32(cmd.ExecuteScalar());

                    string sqlV = "INSERT INTO gestao.veiculos (placa, marca, modelo, ano, id_cliente) VALUES (@p, @ma, @mo, @ano, @id)";
                    using (var cmdV = new NpgsqlCommand(sqlV, conn))
                    {
                        cmdV.Parameters.AddWithValue("p", placa);
                        cmdV.Parameters.AddWithValue("ma", (object)marca ?? DBNull.Value);
                        cmdV.Parameters.AddWithValue("mo", (object)modelo ?? DBNull.Value);
                        cmdV.Parameters.AddWithValue("ano", (object)ano ?? DBNull.Value);
                        cmdV.Parameters.AddWithValue("id", idCliente);
                        cmdV.ExecuteNonQuery();
                    }
                }
            }
            return RedirectToAction("Index");
        }
        catch (Exception ex) { return Content("Erro: " + ex.Message); }
    }

    public IActionResult BuscarCliente(string cpf)
    {
        if (string.IsNullOrEmpty(cpf)) return View();
        cpf = cpf.Replace(".", "").Replace("-", "").Replace(" ", "");

        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            string sql = @"SELECT c.nome, v.id_veiculo, v.placa, v.modelo 
                           FROM gestao.clientes c 
                           JOIN gestao.veiculos v ON c.id_cliente = v.id_cliente 
                           WHERE c.cpf = @cpf LIMIT 1";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("cpf", cpf);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) {
                        ViewBag.Cliente = new { 
                            Nome = reader["nome"], 
                            IdVeiculo = reader["id_veiculo"], 
                            Placa = reader["placa"], 
                            Modelo = reader["modelo"] 
                        };
                    }
                }
            }
        }
        return View();
    }

    public IActionResult BuscarOS(string termo)
    {
        var listaOS = new List<object>();
        string busca = string.IsNullOrWhiteSpace(termo) ? "" : termo.Trim();

        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            string sql = @"SELECT os.id_os, os.data_entrada, os.problema_relatado, os.valor_total, 
                                   c.nome as nome_cliente, v.placa, v.modelo
                           FROM gestao.ordens_servico os
                           JOIN gestao.veiculos v ON os.id_veiculo = v.id_veiculo
                           JOIN gestao.clientes c ON v.id_cliente = c.id_cliente
                           WHERE (@t = '' OR v.placa ILIKE @tp OR c.cpf ILIKE @tp OR c.nome ILIKE @tp OR v.modelo ILIKE @tp)
                           ORDER BY os.id_os DESC";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("t", busca);
                cmd.Parameters.AddWithValue("tp", "%" + busca + "%");

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) {
                        listaOS.Add(new { 
                            Id = reader["id_os"], 
                            Data = Convert.ToDateTime(reader["data_entrada"]).ToString("dd/MM/yyyy"), 
                            Cliente = reader["nome_cliente"], 
                            Veiculo = $"{reader["modelo"]} ({reader["placa"]})",
                            Problema = reader["problema_relatado"], 
                            Total = reader["valor_total"] 
                        });
                    }
                }
            }
        }
        ViewBag.ListaOS = listaOS;
        ViewBag.TermoBusca = termo;
        return View();
    }

    public IActionResult LancarOS(int idVeiculo, string placa)
    {
        ViewBag.IdVeiculo = idVeiculo;
        ViewBag.Placa = placa;
        return View();
    }

    [HttpPost]
    public IActionResult SalvarOS(int idVeiculo, string problema, string solucao, string pecas, decimal valorPecas, decimal valorMaoDeObra)
    {
        try
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO gestao.ordens_servico (id_veiculo, problema_relatado, solucao_tecnica, pecas_itens, valor_pecas, valor_mao_de_obra) 
                               VALUES (@idV, @prob, @sol, @pec, @vP, @vM)";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("idV", idVeiculo);
                    cmd.Parameters.AddWithValue("prob", problema);
                    cmd.Parameters.AddWithValue("sol", solucao);
                    cmd.Parameters.AddWithValue("pec", (object)pecas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("vP", valorPecas);
                    cmd.Parameters.AddWithValue("vM", valorMaoDeObra);
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("BuscarOS");
        }
        catch (Exception ex) { return Content("Erro ao salvar O.S.: " + ex.Message); }
    }
}